namespace OcrSnip.Ocr;

public static class OcrTextFormatter
{
    public static IReadOnlyList<OcrLine> SortLines(IReadOnlyList<OcrLine> lines)
    {
        if (lines.Count <= 1)
        {
            return lines;
        }

        var medianHeight = lines.Select(OcrGeometry.LineHeight).Order().ElementAt(lines.Count / 2);
        return lines
            .OrderBy(line => Math.Round(OcrGeometry.CenterY(line.Bounds) / Math.Max(1, medianHeight * 0.6f)))
            .ThenBy(line => line.Bounds.TopLeft.X)
            .ToArray();
    }

    public static string FormatLines(IReadOnlyList<OcrLine> lines, OcrCopyMode copyMode)
    {
        var rows = GroupRows(lines);
        var layout = AnalyzeLayout(rows);
        if (copyMode == OcrCopyMode.Raw)
        {
            rows = RestoreMissingBulletMarkers(rows, layout);
            rows = rows.Where(row => !IsLikelyPhoneIconNoiseRow(row)).ToArray();
        }

        if (copyMode == OcrCopyMode.Raw && IsPhoneMessageLikeRows(rows))
        {
            return FormatSingleFlowRows(rows, layout);
        }

        if (copyMode == OcrCopyMode.Raw && IsConversationLikeRows(rows))
        {
            return FormatSingleFlowRows(rows, layout);
        }

        if (copyMode == OcrCopyMode.Raw && TryFormatLocalMenuSections(rows, layout, out var localMenuText))
        {
            return localMenuText;
        }

        if (copyMode == OcrCopyMode.Raw && IsSymbolHeavyCapture(lines))
        {
            return FormatSpatialRows(rows);
        }

        if (copyMode == OcrCopyMode.Raw && IsTableLikeRows(rows))
        {
            return FormatTableRows(rows, layout);
        }

        if (copyMode == OcrCopyMode.Raw && TryFormatMultiColumnRows(rows, layout, out var multiColumnText))
        {
            return multiColumnText;
        }

        if (copyMode == OcrCopyMode.Raw && TryMergeGlobalDetachedPriceRows(rows, out var globallyMergedPriceRows))
        {
            return FormatSingleFlowRows(globallyMergedPriceRows, layout);
        }

        var sectionMergedRows = MergeDetachedPriceRows(rows);
        if (!RowsReferenceEqual(sectionMergedRows, rows))
        {
            return FormatSingleFlowRows(sectionMergedRows, layout);
        }

        var formatted = new List<string>();
        var medianHeight = Median(rows.SelectMany(row => row).Select(line => Height(line.Bounds)));
        IReadOnlyList<OcrLine>? previousNonEmptyRow = null;
        var skippedEmptyRow = false;
        for (var i = 0; i < rows.Count; i++)
        {
            var text = FormatRow(rows[i], copyMode, layout);
            if (text.Length == 0)
            {
                skippedEmptyRow = true;
                continue;
            }

            if (previousNonEmptyRow is not null && !skippedEmptyRow && ShouldInsertBlankLine(previousNonEmptyRow, rows[i], layout, medianHeight))
            {
                formatted.Add(string.Empty);
            }

            formatted.Add(text);
            previousNonEmptyRow = rows[i];
            skippedEmptyRow = false;
        }

        return string.Join(Environment.NewLine, formatted);
    }

    private static string FormatSingleFlowRows(IReadOnlyList<IReadOnlyList<OcrLine>> rows, LayoutContext layout)
    {
        var formatted = new List<string>();
        var medianHeight = Median(rows.SelectMany(row => row).Select(line => Height(line.Bounds)));
        IReadOnlyList<OcrLine>? previousNonEmptyRow = null;
        foreach (var row in rows)
        {
            AddFormattedRow(formatted, row, layout, medianHeight, ref previousNonEmptyRow);
        }

        return string.Join(Environment.NewLine, formatted);
    }

    private static bool TryFormatLocalMenuSections(IReadOnlyList<IReadOnlyList<OcrLine>> rows, LayoutContext layout, out string text)
    {
        text = string.Empty;
        var allLines = rows.SelectMany(row => row).ToArray();
        var headingLines = rows
            .SelectMany((row, rowIndex) => row.Select(line => new { Line = line, RowIndex = rowIndex }))
            .Where(item => IsAllCapsSectionFragment(item.Line) && !HasNearbyRightPrice(item.Line, allLines))
            .OrderBy(item => Top(item.Line.Bounds))
            .ThenBy(item => Left(item.Line.Bounds))
            .ToArray();
        if (headingLines.Length < 4 || rows.SelectMany(row => row).Count(line => IsPriceText(line.Text)) < 3)
        {
            return false;
        }

        var medianHeight = Median(rows.SelectMany(row => row).Select(line => Height(line.Bounds)));
        var headingLevels = GroupHeadingLevels(headingLines.Select(item => item.Line).ToArray(), medianHeight);
        if (headingLevels.Count < 4 || !headingLevels.Any(level => level.Count > 1))
        {
            return false;
        }

        var formatted = new List<string>();
        for (var levelIndex = 0; levelIndex < headingLevels.Count; levelIndex++)
        {
            var level = headingLevels[levelIndex].OrderBy(line => Left(line.Bounds)).ToArray();
            var levelTop = level.Min(line => Top(line.Bounds));
            var levelBottom = level.Max(line => Bottom(line.Bounds));
            var nextLevelTop = levelIndex + 1 < headingLevels.Count
                ? headingLevels[levelIndex + 1].Min(line => Top(line.Bounds))
                : float.PositiveInfinity;
            var sectionRows = rows
                .Where(row => row.All(line => !level.Contains(line)))
                .Where(row =>
                {
                    var top = row.Min(line => Top(line.Bounds));
                    return top > levelBottom && top < nextLevelTop;
                })
                .ToArray();

            var sectionLines = sectionRows.SelectMany(row => row).ToArray();
            for (var headingIndex = 0; headingIndex < level.Length; headingIndex++)
            {
                var heading = level[headingIndex];
                if (formatted.Count > 0 && formatted[^1].Length > 0)
                {
                    formatted.Add(string.Empty);
                }

                formatted.Add(heading.Text.Trim());
                var rowsForSection = MergeDetachedPriceRows(GroupRows(LinesInSectionSpan(sectionLines, level, headingIndex)));
                IReadOnlyList<OcrLine>? previous = null;
                foreach (var row in rowsForSection)
                {
                    AddFormattedRow(formatted, row, layout, medianHeight, ref previous, suppressAttachedMetadataBlank: true);
                }
            }
        }

        text = string.Join(Environment.NewLine, formatted.Where((line, index) => line.Length > 0 || (index > 0 && index < formatted.Count - 1)));
        return true;
    }

    private static List<List<OcrLine>> GroupHeadingLevels(IReadOnlyList<OcrLine> headings, float medianHeight)
    {
        var levels = new List<List<OcrLine>>();
        foreach (var heading in headings.OrderBy(line => Top(line.Bounds)).ThenBy(line => Left(line.Bounds)))
        {
            var level = levels.FirstOrDefault(item => Math.Abs(Median(item.Select(line => Top(line.Bounds))) - Top(heading.Bounds)) <= Math.Max(8, medianHeight));
            if (level is null)
            {
                levels.Add([heading]);
                continue;
            }

            level.Add(heading);
        }

        return levels
            .OrderBy(level => level.Min(line => Top(line.Bounds)))
            .ToList();
    }

    private static IReadOnlyList<OcrLine> LinesInSectionSpan(IReadOnlyList<OcrLine> lines, IReadOnlyList<OcrLine> headings, int headingIndex)
    {
        var heading = headings[headingIndex];
        var leftBound = headingIndex == 0
            ? float.NegativeInfinity
            : Left(heading.Bounds) - 8;
        var rightBound = headingIndex + 1 < headings.Count
            ? Left(headings[headingIndex + 1].Bounds) - 8
            : float.PositiveInfinity;
        if (headings.Count == 1)
        {
            leftBound = float.NegativeInfinity;
            rightBound = float.PositiveInfinity;
        }

        return lines
            .Where(line =>
            {
                var centerX = (Left(line.Bounds) + Right(line.Bounds)) / 2;
                return centerX >= leftBound && centerX < rightBound;
            })
            .OrderBy(line => Top(line.Bounds))
            .ThenBy(line => Left(line.Bounds))
            .ToArray();
    }

    private static bool HasNearbyRightPrice(OcrLine line, IReadOnlyList<OcrLine> allLines)
    {
        var center = CenterY(line.Bounds);
        var tolerance = Math.Max(16, Height(line.Bounds) * 1.1f);
        return allLines.Any(candidate =>
            IsPriceText(candidate.Text)
            && Left(candidate.Bounds) > Right(line.Bounds)
            && Math.Abs(CenterY(candidate.Bounds) - center) <= tolerance);
    }

    private static bool IsTableLikeRows(IReadOnlyList<IReadOnlyList<OcrLine>> rows)
    {
        var dataRows = rows
            .Where(row => row.Count >= 3)
            .ToArray();
        if (dataRows.Length < 5)
        {
            return false;
        }

        var rowsWithLongMiddleText = dataRows.Count(row =>
        {
            var ordered = row.OrderBy(line => Left(line.Bounds)).ToArray();
            return ordered.Skip(1).Take(Math.Max(1, ordered.Length - 2)).Any(line => line.Text.Trim().Length >= 14 || Width(line.Bounds) >= 120);
        });
        if (rowsWithLongMiddleText < 4)
        {
            return false;
        }

        var rowTops = dataRows.Select(row => row.Min(line => Top(line.Bounds))).Order().ToArray();
        var gaps = rowTops.Zip(rowTops.Skip(1), (first, second) => second - first).Where(gap => gap > 0).ToArray();
        if (gaps.Length == 0)
        {
            return false;
        }

        var medianGap = Median(gaps);
        return gaps.Count(gap => Math.Abs(gap - medianGap) <= Math.Max(6, medianGap * 0.35f)) >= gaps.Length * 0.7;
    }

    private static bool IsConversationLikeRows(IReadOnlyList<IReadOnlyList<OcrLine>> rows)
    {
        if (rows.SelectMany(row => row).Count(line => IsPriceText(line.Text)) >= 2)
        {
            return false;
        }

        var singleRows = rows
            .Where(row => row.Count == 1)
            .Select(row => row[0])
            .Where(IsConversationCandidateLine)
            .OrderBy(line => Top(line.Bounds))
            .ToArray();
        if (singleRows.Length < 4)
        {
            return false;
        }

        if (singleRows.Any(line => IsPriceText(line.Text) || IsLikelyListMarker(line)))
        {
            return false;
        }

        var clusters = ClusterLeftEdges(singleRows.Select(line => Left(line.Bounds)).ToArray(), tolerance: 44);
        if (clusters.Length < 2)
        {
            return false;
        }

        var medianHeight = Median(singleRows.Select(line => Height(line.Bounds)));
        var blocks = new List<int>();
        OcrLine? previous = null;
        var previousCluster = -1;
        foreach (var line in singleRows)
        {
            var cluster = FindNearestCluster(Left(line.Bounds), clusters);
            var sameBubbleContinuation = previous is not null
                && cluster == previousCluster
                && Top(line.Bounds) - Bottom(previous.Bounds) <= Math.Max(10, medianHeight * 1.6f);
            if (!sameBubbleContinuation)
            {
                blocks.Add(cluster);
            }

            previous = line;
            previousCluster = cluster;
        }

        if (blocks.Count < 4)
        {
            return false;
        }

        var transitions = blocks.Zip(blocks.Skip(1), (first, second) => first != second).Count(changed => changed);
        return transitions >= 2;
    }

    private static bool IsPhoneMessageLikeRows(IReadOnlyList<IReadOnlyList<OcrLine>> rows)
    {
        var lines = rows.SelectMany(row => row).ToArray();
        if (lines.Length < 5 || lines.Any(line => IsPriceText(line.Text)))
        {
            return false;
        }

        var hasMessageChrome = lines.Any(line =>
        {
            var text = line.Text.Trim();
            return text.Equals("Delivered", StringComparison.OrdinalIgnoreCase)
                || text.Equals("iMessage", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Today ", StringComparison.OrdinalIgnoreCase);
        });
        var hasLongBubbleLine = lines.Any(line => Width(line.Bounds) >= 160 && line.Text.Trim().Contains(' ', StringComparison.Ordinal));
        return hasMessageChrome && hasLongBubbleLine;
    }

    private static bool IsConversationCandidateLine(OcrLine line)
    {
        var text = line.Text.Trim();
        if (text.Length < 4 || IsPriceText(text) || IsLikelyListMarker(line) || !text.Any(char.IsLetter))
        {
            return false;
        }

        if (Width(line.Bounds) >= 55)
        {
            return true;
        }

        return text.Contains(' ', StringComparison.Ordinal)
            || text.Any(character => character is '.' or ',' or '?' or '!' or '\'');
    }

    private static string FormatTableRows(IReadOnlyList<IReadOnlyList<OcrLine>> rows, LayoutContext layout)
    {
        var formatted = new List<string>();
        var seenDataRow = false;
        foreach (var row in rows)
        {
            var rowText = FormatRow(row, OcrCopyMode.Raw, layout);
            if (rowText.Length == 0)
            {
                continue;
            }

            var isDataRow = row.Count >= 3;
            if (!seenDataRow && isDataRow && formatted.Count > 0 && formatted[^1].Length > 0)
            {
                formatted.Add(string.Empty);
            }

            formatted.Add(rowText);
            seenDataRow |= isDataRow;
        }

        return string.Join(Environment.NewLine, formatted);
    }

    private static bool TryFormatMultiColumnRows(IReadOnlyList<IReadOnlyList<OcrLine>> rows, LayoutContext layout, out string text)
    {
        text = string.Empty;
        var columnStarts = DetectColumnStarts(rows);
        if (columnStarts.Length < 2)
        {
            return false;
        }

        if (!HasSideBySideColumnEvidence(rows, columnStarts))
        {
            return false;
        }

        var medianHeight = Median(rows.SelectMany(row => row).Select(line => Height(line.Bounds)));
        var bands = GroupBands(rows, medianHeight);
        var formatted = new List<string>();
        IReadOnlyList<OcrLine>? previousNonEmptyRow = null;
        foreach (var band in bands)
        {
            var columnRows = SplitRowsIntoColumns(band, columnStarts);
            var activeColumns = columnRows
                .Select((column, index) => new { column, index })
                .Where(item => item.column.Count > 0)
                .ToArray();

            if (activeColumns.Length <= 1)
            {
                if (formatted.Count > 0 && formatted[^1].Length > 0)
                {
                    formatted.Add(string.Empty);
                }

                foreach (var row in GroupRows(band))
                {
                    var rowText = FormatRow(row, OcrCopyMode.Raw, layout);
                    if (rowText.Length > 0)
                    {
                        formatted.Add(rowText);
                    }
                }

                previousNonEmptyRow = band;
                continue;
            }

            if (formatted.Count > 0 && formatted[^1].Length > 0)
            {
                formatted.Add(string.Empty);
            }

            for (var i = 0; i < activeColumns.Length; i++)
            {
                if (i + 1 < activeColumns.Length
                    && TryMergeAdjacentMenuPriceColumns(activeColumns[i].column, activeColumns[i + 1].column, out var mergedColumnRows))
                {
                    mergedColumnRows = MergeDetachedPriceRows(mergedColumnRows);
                    IReadOnlyList<OcrLine>? previousMergedColumnRow = null;
                    foreach (var row in mergedColumnRows)
                    {
                        AddFormattedRow(formatted, row, layout, medianHeight, ref previousMergedColumnRow, suppressAttachedMetadataBlank: true);
                    }

                    i++;
                    if (i < activeColumns.Length - 1 && formatted.Count > 0 && formatted[^1].Length > 0)
                    {
                        formatted.Add(string.Empty);
                    }

                    continue;
                }

                var rowsForColumn = MergeDetachedPriceRows(activeColumns[i].column);
                IReadOnlyList<OcrLine>? previousColumnRow = null;
                foreach (var row in rowsForColumn)
                {
                    AddFormattedRow(formatted, row, layout, medianHeight, ref previousColumnRow, suppressAttachedMetadataBlank: true);
                }

                if (i < activeColumns.Length - 1 && formatted.Count > 0 && formatted[^1].Length > 0)
                {
                    formatted.Add(string.Empty);
                }
            }

            previousNonEmptyRow = band;
        }

        text = string.Join(Environment.NewLine, formatted);
        return true;
    }

    private static bool HasSideBySideColumnEvidence(IReadOnlyList<IReadOnlyList<OcrLine>> rows, float[] columnStarts)
    {
        return rows.Count(row =>
        {
            var columns = row
                .Where(line => IsColumnStartCandidate(line))
                .Select(line => FindColumnIndex(line, columnStarts))
                .Distinct()
                .Count();
            return columns >= 2;
        }) >= 2;
    }

    private static bool TryMergeAdjacentMenuPriceColumns(
        IReadOnlyList<IReadOnlyList<OcrLine>> nameColumn,
        IReadOnlyList<IReadOnlyList<OcrLine>> priceColumn,
        out IReadOnlyList<IReadOnlyList<OcrLine>> mergedRows)
    {
        mergedRows = [];
        var prices = priceColumn.Where(IsPriceOnlyRow).ToArray();
        if (prices.Length < 2 || prices.Length != priceColumn.Count)
        {
            return false;
        }

        if (TryMergeYAlignedMenuPriceColumns(nameColumn, prices, out mergedRows))
        {
            return true;
        }

        var result = new List<IReadOnlyList<OcrLine>>();
        var pendingItems = new List<IReadOnlyList<OcrLine>>();
        var priceIndex = 0;
        foreach (var row in nameColumn)
        {
            if (IsDetachedMenuItemRow(row) && priceIndex < prices.Length)
            {
                pendingItems.Add(row);
                continue;
            }

            FlushPendingMenuItems(result, pendingItems, prices, ref priceIndex);
            result.Add(row);
        }

        FlushPendingMenuItems(result, pendingItems, prices, ref priceIndex);
        if (priceIndex != prices.Length)
        {
            return false;
        }

        mergedRows = result;
        return true;
    }

    private static bool TryMergeYAlignedMenuPriceColumns(
        IReadOnlyList<IReadOnlyList<OcrLine>> nameColumn,
        IReadOnlyList<IReadOnlyList<OcrLine>> prices,
        out IReadOnlyList<IReadOnlyList<OcrLine>> mergedRows)
    {
        mergedRows = [];
        var candidates = nameColumn
            .Select((row, index) => new { Row = row, Index = index })
            .Where(item => IsMenuItemNameRow(item.Row))
            .ToArray();
        if (candidates.Length < prices.Count)
        {
            return false;
        }

        var medianHeight = Median(nameColumn.Concat(prices).SelectMany(row => row).Select(line => Height(line.Bounds)));
        var maxDistance = Math.Max(18, medianHeight * 1.75f);
        var selected = new Dictionary<int, int>();
        var previousCandidateIndex = -1;
        for (var priceIndex = 0; priceIndex < prices.Count; priceIndex++)
        {
            var priceCenter = CenterY(prices[priceIndex][0].Bounds);
            var best = candidates
                .Where(candidate => candidate.Index > previousCandidateIndex)
                .Select(candidate => new
                {
                    candidate.Index,
                    candidate.Row,
                    Distance = Math.Abs(Median(candidate.Row.Select(line => CenterY(line.Bounds))) - priceCenter)
                })
                .Where(candidate => candidate.Distance <= maxDistance)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Index)
                .FirstOrDefault();
            if (best is null)
            {
                return false;
            }

            selected[best.Index] = priceIndex;
            previousCandidateIndex = best.Index;
        }

        var result = new List<IReadOnlyList<OcrLine>>();
        for (var i = 0; i < nameColumn.Count; i++)
        {
            result.Add(selected.TryGetValue(i, out var priceIndex)
                ? MergeItemAndPriceRows(nameColumn[i], prices[priceIndex])
                : nameColumn[i]);
        }

        mergedRows = result;
        return true;
    }

    private static void FlushPendingMenuItems(
        List<IReadOnlyList<OcrLine>> result,
        List<IReadOnlyList<OcrLine>> pendingItems,
        IReadOnlyList<IReadOnlyList<OcrLine>> prices,
        ref int priceIndex)
    {
        if (pendingItems.Count == 0)
        {
            return;
        }

        if (priceIndex + pendingItems.Count > prices.Count)
        {
            foreach (var item in pendingItems)
            {
                result.Add(item);
            }

            pendingItems.Clear();
            return;
        }

        foreach (var item in pendingItems)
        {
            result.Add(MergeItemAndPriceRows(item, prices[priceIndex]));
            priceIndex++;
        }

        pendingItems.Clear();
    }

    private static IReadOnlyList<IReadOnlyList<OcrLine>> MergeDetachedPriceRows(IReadOnlyList<IReadOnlyList<OcrLine>> rows)
    {
        if (TryMergeGlobalDetachedPriceRows(rows, out var globallyMerged))
        {
            return globallyMerged;
        }

        var result = new List<IReadOnlyList<OcrLine>>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (IsHeadingRow(rows[i]) && TryCollectMenuItemPriceRun(rows, i + 1, out var itemRows, out var priceRows, out var consumed))
            {
                result.Add(rows[i]);
                for (var j = 0; j < itemRows.Count; j++)
                {
                    result.Add(MergeItemAndPriceRows(itemRows[j], priceRows[j]));
                }

                i += consumed;
                continue;
            }

            result.Add(rows[i]);
        }

        return result;
    }

    private static bool RowsReferenceEqual(IReadOnlyList<IReadOnlyList<OcrLine>> first, IReadOnlyList<IReadOnlyList<OcrLine>> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var i = 0; i < first.Count; i++)
        {
            if (!ReferenceEquals(first[i], second[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMergeGlobalDetachedPriceRows(
        IReadOnlyList<IReadOnlyList<OcrLine>> rows,
        out IReadOnlyList<IReadOnlyList<OcrLine>> mergedRows)
    {
        mergedRows = [];
        var priceRows = rows.Where(IsPriceOnlyRow).ToArray();
        if (priceRows.Length < 3)
        {
            return false;
        }

        var firstPriceIndex = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (IsPriceOnlyRow(rows[i]))
            {
                firstPriceIndex = i;
                break;
            }
        }

        if (firstPriceIndex <= 0)
        {
            return false;
        }

        var priceEndIndex = firstPriceIndex;
        while (priceEndIndex < rows.Count && IsPriceOnlyRow(rows[priceEndIndex]))
        {
            priceEndIndex++;
        }

        if (rows.Skip(priceEndIndex).Any(IsDetachedMenuItemRow))
        {
            return false;
        }

        priceRows = rows.Skip(firstPriceIndex).Take(priceEndIndex - firstPriceIndex).ToArray();
        var itemRows = rows.Take(firstPriceIndex).Where(IsDetachedMenuItemRow).ToArray();
        if (itemRows.Length != priceRows.Length)
        {
            return false;
        }

        var result = new List<IReadOnlyList<OcrLine>>();
        var priceIndex = 0;
        for (var i = 0; i < firstPriceIndex; i++)
        {
            var row = rows[i];
            if (IsDetachedMenuItemRow(row))
            {
                result.Add(MergeItemAndPriceRows(row, priceRows[priceIndex]));
                priceIndex++;
                continue;
            }

            result.Add(row);
        }

        for (var i = priceEndIndex; i < rows.Count; i++)
        {
            result.Add(rows[i]);
        }

        mergedRows = result;
        return true;
    }

    private static IReadOnlyList<OcrLine> MergeItemAndPriceRows(IReadOnlyList<OcrLine> itemRow, IReadOnlyList<OcrLine> priceRow)
    {
        var itemTop = itemRow.Min(line => Top(line.Bounds));
        var itemHeight = Median(itemRow.Select(line => Height(line.Bounds)));
        var itemRight = itemRow.Max(line => Right(line.Bounds));
        var charWidth = EstimateCharacterWidth(itemRow);
        var alignedPrices = priceRow.Select(line =>
        {
            var originalLeft = Left(line.Bounds);
            var originalRight = Right(line.Bounds);
            var priceWidth = Math.Max(1, originalRight - originalLeft);
            var minimumGap = Math.Max(24, charWidth * 4);
            var left = originalLeft - itemRight < minimumGap
                ? itemRight + minimumGap
                : originalLeft;
            var right = left + priceWidth;
            return line with
            {
                Bounds = new OcrQuadrilateral(
                    new OcrPoint(left, itemTop),
                    new OcrPoint(right, itemTop),
                    new OcrPoint(right, itemTop + itemHeight),
                    new OcrPoint(left, itemTop + itemHeight))
            };
        });

        return itemRow.Concat(alignedPrices).OrderBy(line => Left(line.Bounds)).ToArray();
    }

    private static bool TryCollectMenuItemPriceRun(
        IReadOnlyList<IReadOnlyList<OcrLine>> rows,
        int start,
        out List<IReadOnlyList<OcrLine>> itemRows,
        out List<IReadOnlyList<OcrLine>> priceRows,
        out int consumed)
    {
        itemRows = [];
        priceRows = [];
        consumed = 0;
        var i = start;
        while (i < rows.Count && IsMenuItemNameRow(rows[i]))
        {
            itemRows.Add(rows[i]);
            i++;
        }

        while (i < rows.Count && IsPriceOnlyRow(rows[i]))
        {
            priceRows.Add(rows[i]);
            i++;
        }

        consumed = i - start;
        return itemRows.Count >= 2 && itemRows.Count == priceRows.Count;
    }

    private static bool IsMenuItemNameRow(IReadOnlyList<OcrLine> row)
    {
        if (row.Count != 1)
        {
            return false;
        }

        var text = row[0].Text.Trim();
        return text.Length >= 3
            && text.Any(char.IsLetter)
            && !LooksLikePriceText(text)
            && !LooksLikeUrlOrFileFooter(text);
    }

    private static bool LooksLikeUrlOrFileFooter(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Contains("://", StringComparison.Ordinal)
            || trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            || (trimmed.Count(character => character == '.') >= 1
                && trimmed.Any(char.IsLetter)
                && trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() is { Length: >= 2 and <= 6 } suffix
                && suffix.All(char.IsLetter));
    }

    private static bool IsDetachedMenuItemRow(IReadOnlyList<OcrLine> row)
    {
        if (!IsMenuItemNameRow(row))
        {
            return false;
        }

        return !IsAllCapsText(row[0].Text);
    }

    private static bool IsPriceOnlyRow(IReadOnlyList<OcrLine> row)
    {
        return row.Count == 1 && IsPriceText(row[0].Text);
    }

    private static bool IsPriceText(string text)
    {
        var trimmed = text.Trim();
        return LooksLikePriceText(trimmed)
            && trimmed.Count(character => character == '$') == 1
            && trimmed.Count(char.IsDigit) >= 1;
    }

    private static bool LooksLikePriceText(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length is >= 2 and <= 14
            && trimmed.Contains('$', StringComparison.Ordinal)
            && trimmed.Any(char.IsDigit)
            && trimmed.All(character => char.IsDigit(character) || character is '$' or '.' or ',' or ' ');
    }

    private static void AddFormattedRow(List<string> formatted, IReadOnlyList<OcrLine> row, LayoutContext layout, float medianHeight, ref IReadOnlyList<OcrLine>? previousNonEmptyRow, bool suppressAttachedMetadataBlank = false)
    {
        var rowText = FormatRow(row, OcrCopyMode.Raw, layout);
        if (rowText.Length == 0)
        {
            return;
        }

        if (previousNonEmptyRow is not null
            && ShouldInsertBlankLine(previousNonEmptyRow, row, layout, medianHeight)
            && !(suppressAttachedMetadataBlank && IsAttachedMetadataRow(previousNonEmptyRow, row)))
        {
            formatted.Add(string.Empty);
        }

        formatted.Add(rowText);
        previousNonEmptyRow = row;
    }

    private static float[] DetectColumnStarts(IReadOnlyList<IReadOnlyList<OcrLine>> rows)
    {
        var candidates = rows
            .SelectMany(row => row)
            .Where(IsColumnStartCandidate)
            .Select(line => new ColumnCandidate(Left(line.Bounds), line.Text.Trim(), Width(line.Bounds)))
            .OrderBy(candidate => candidate.Left)
            .ToArray();
        if (candidates.Length < 5)
        {
            return [];
        }

        var clusters = new List<List<ColumnCandidate>>();
        foreach (var candidate in candidates)
        {
            var cluster = clusters.FirstOrDefault(item => Math.Abs(Median(item.Select(value => value.Left)) - candidate.Left) <= 36);
            if (cluster is null)
            {
                clusters.Add([candidate]);
                continue;
            }

            cluster.Add(candidate);
        }

        var starts = clusters
            .Where(IsColumnStartCluster)
            .Select(cluster => Median(cluster.Select(candidate => candidate.Left)))
            .Order()
            .ToArray();
        if (starts.Length < 2)
        {
            return [];
        }

        var wideStarts = new List<float> { starts[0] };
        foreach (var start in starts.Skip(1))
        {
            if (start - wideStarts[^1] >= 55)
            {
                wideStarts.Add(start);
            }
        }

        return wideStarts.Count >= 2 ? wideStarts.ToArray() : [];
    }

    private static bool IsColumnStartCluster(IReadOnlyList<ColumnCandidate> cluster)
    {
        if (cluster.Count < 2)
        {
            return false;
        }

        var uniqueTextCount = cluster
            .Select(candidate => candidate.Text.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (cluster.Count >= 5 && uniqueTextCount >= 3)
        {
            return true;
        }

        return cluster.Count >= 2 && uniqueTextCount >= 2 && Median(cluster.Select(candidate => candidate.Width)) >= 55;
    }

    private static bool IsColumnStartCandidate(OcrLine line)
    {
        var text = line.Text.Trim();
        if (text.Length < 2 || IsLikelyListMarker(line))
        {
            return false;
        }

        return text.Any(char.IsLetterOrDigit) && text.Length >= 4 && Width(line.Bounds) >= 28;
    }

    private static IReadOnlyList<IReadOnlyList<OcrLine>> GroupBands(IReadOnlyList<IReadOnlyList<OcrLine>> rows, float medianHeight)
    {
        var bands = new List<List<OcrLine>>();
        foreach (var row in rows)
        {
            var rowTop = row.Min(line => Top(line.Bounds));
            var previous = bands.LastOrDefault();
            var gap = previous is null ? 0 : rowTop - previous.Max(line => Bottom(line.Bounds));
            if (previous is null || (gap > Math.Max(32, medianHeight * 2.6f) && !(gap <= 100 && IsShortMetadataRow(row))))
            {
                bands.Add([.. row]);
                continue;
            }

            previous.AddRange(row);
        }

        return bands
            .Select(band => (IReadOnlyList<OcrLine>)band.OrderBy(line => Top(line.Bounds)).ThenBy(line => Left(line.Bounds)).ToArray())
            .ToArray();
    }

    private static bool IsShortMetadataRow(IReadOnlyList<OcrLine> row)
    {
        if (row.Count == 0 || row.Count > 2)
        {
            return false;
        }

        return row.All(line =>
        {
            var text = line.Text.Trim();
            return text.Length is > 0 and <= 18 && Width(line.Bounds) <= 120;
        });
    }

    private static bool IsAttachedMetadataRow(IReadOnlyList<OcrLine> previous, IReadOnlyList<OcrLine> current)
    {
        if (!IsShortMetadataRow(current))
        {
            return false;
        }

        var previousText = string.Join(" ", previous.Select(line => line.Text.Trim())).Trim();
        return IsHeadingRow(previous) || previousText.EndsWith(".", StringComparison.Ordinal);
    }

    private static IReadOnlyList<IReadOnlyList<OcrLine>>[] SplitRowsIntoColumns(IReadOnlyList<OcrLine> band, float[] columnStarts)
    {
        var columnLines = columnStarts.Select(_ => new List<OcrLine>()).ToArray();
        foreach (var line in band)
        {
            columnLines[FindColumnIndex(line, columnStarts)].Add(line);
        }

        return columnLines
            .Select(lines => GroupRows(lines))
            .ToArray();
    }

    private static int FindColumnIndex(OcrLine line, float[] columnStarts)
    {
        for (var i = 0; i < columnStarts.Length - 1; i++)
        {
            var boundary = columnStarts[i] + ((columnStarts[i + 1] - columnStarts[i]) * 0.9f);
            if (Left(line.Bounds) < boundary)
            {
                return i;
            }
        }

        return columnStarts.Length - 1;
    }

    private static float[] ClusterLeftEdges(float[] leftEdges, float tolerance)
    {
        var clusters = new List<List<float>>();
        foreach (var left in leftEdges.Order())
        {
            var cluster = clusters.FirstOrDefault(item => Math.Abs(Median(item) - left) <= tolerance);
            if (cluster is null)
            {
                clusters.Add([left]);
                continue;
            }

            cluster.Add(left);
        }

        return clusters
            .Where(cluster => cluster.Count >= 2)
            .Select(Median)
            .Order()
            .ToArray();
    }

    private static int FindNearestCluster(float left, float[] clusters)
    {
        var bestIndex = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < clusters.Length; i++)
        {
            var distance = Math.Abs(left - clusters[i]);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static IReadOnlyList<IReadOnlyList<OcrLine>> GroupRows(IReadOnlyList<OcrLine> lines)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var ordered = lines
            .OrderBy(line => Top(line.Bounds))
            .ThenBy(line => Left(line.Bounds))
            .ToArray();
        var rows = new List<List<OcrLine>>();
        foreach (var line in ordered)
        {
            var row = rows.FirstOrDefault(candidate => SameRow(candidate, line));
            if (row is null)
            {
                rows.Add([line]);
                continue;
            }

            row.Add(line);
        }

        return rows
            .OrderBy(row => row.Min(line => Top(line.Bounds)))
            .Select(row => (IReadOnlyList<OcrLine>)row.OrderBy(line => Left(line.Bounds)).ToArray())
            .ToArray();
    }

    private static bool SameRow(IReadOnlyList<OcrLine> row, OcrLine line)
    {
        var textFragments = row.Where(item => !IsLikelyListMarker(item)).ToArray();
        if (textFragments.Length > 0)
        {
            var sameColumnFragments = textFragments.Where(item => IsSameColumn(item, line)).ToArray();
            if (sameColumnFragments.Length > 0)
            {
                return sameColumnFragments.Any(item => SameVisualRow(item, line));
            }

            return textFragments.Any(item => SameVisualRow(item, line));
        }

        return row.Any(item => SameVisualRow(item, line));
    }

    private static LayoutContext AnalyzeLayout(IReadOnlyList<IReadOnlyList<OcrLine>> rows)
    {
        var markerRows = rows
            .Where(row => row.Count >= 2 && IsLikelyListMarker(row[0]))
            .Select(row => new
            {
                MarkerLeft = Left(row[0].Bounds),
                MarkerRight = Right(row[0].Bounds),
                MarkerText = row[0].Text.Trim(),
                TextLeft = Left(row[1].Bounds),
                CharWidth = EstimateCharacterWidth(row)
            })
            .ToArray();
        if (markerRows.Length == 0)
        {
            return new LayoutContext(false, 0, 0, 8, string.Empty);
        }

        return new LayoutContext(
            HasListColumns: true,
            MarkerLeft: Median(markerRows.Select(row => row.MarkerLeft)),
            TextLeft: Median(markerRows.Select(row => row.TextLeft)),
            CharWidth: Median(markerRows.Select(row => row.CharWidth)),
            MarkerText: markerRows
                .GroupBy(row => row.MarkerText, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .First().Key);
    }

    private static IReadOnlyList<IReadOnlyList<OcrLine>> RestoreMissingBulletMarkers(IReadOnlyList<IReadOnlyList<OcrLine>> rows, LayoutContext layout)
    {
        var restore = FindMissingBulletRows(rows, layout);
        if (restore.Count == 0)
        {
            return rows;
        }

        var result = new List<IReadOnlyList<OcrLine>>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (restore.TryGetValue(i, out var context))
            {
                var text = rows[i][0];
                var markerWidth = Math.Max(4, context.CharWidth);
                var marker = text with
                {
                    Text = context.MarkerText,
                    Bounds = new OcrQuadrilateral(
                        new OcrPoint(context.MarkerLeft, Top(text.Bounds)),
                        new OcrPoint(context.MarkerLeft + markerWidth, Top(text.Bounds)),
                        new OcrPoint(context.MarkerLeft + markerWidth, Bottom(text.Bounds)),
                        new OcrPoint(context.MarkerLeft, Bottom(text.Bounds)))
                };
                result.Add(new[] { marker, text });
                continue;
            }

            result.Add(rows[i]);
        }

        return result;
    }

    private static Dictionary<int, BulletRecoveryContext> FindMissingBulletRows(IReadOnlyList<IReadOnlyList<OcrLine>> rows, LayoutContext layout)
    {
        var result = new Dictionary<int, BulletRecoveryContext>();
        for (var start = 0; start < rows.Count; start++)
        {
            if (!IsHeadingRow(rows[start]))
            {
                continue;
            }

            var end = start + 1;
            while (end < rows.Count && !IsHeadingRow(rows[end]))
            {
                end++;
            }

            var blockIndexes = Enumerable.Range(start + 1, end - start - 1).ToArray();
            if (blockIndexes.Length < 3)
            {
                continue;
            }

            var explicitBulletRows = blockIndexes
                .Where(index => IsExplicitBulletListRow(rows[index], layout))
                .ToArray();
            var hasExplicitBullets = layout.HasListColumns && IsBulletMarkerText(layout.MarkerText) && explicitBulletRows.Length > 0;
            var textLeft = hasExplicitBullets
                ? layout.TextLeft
                : InferAlignedListTextLeft(blockIndexes.Select(index => rows[index]).ToArray());
            if (float.IsNaN(textLeft))
            {
                continue;
            }

            var candidates = blockIndexes
                .Where(index => IsMissingBulletCandidate(rows[index], textLeft, layout.CharWidth))
                .ToArray();
            var candidateCount = candidates.Length + explicitBulletRows.Length;
            if (hasExplicitBullets)
            {
                if (candidateCount < 3)
                {
                    continue;
                }
            }
            else if (candidateCount < 4 || !HasRegularVerticalRhythm(candidates.Select(index => rows[index]).ToArray()))
            {
                continue;
            }

            var charWidth = hasExplicitBullets
                ? layout.CharWidth
                : Median(candidates.Select(index => EstimateCharacterWidth(rows[index])));
            var markerLeft = hasExplicitBullets
                ? layout.MarkerLeft
                : textLeft - Math.Max(14, charWidth * 2);
            var markerText = hasExplicitBullets ? layout.MarkerText : "â€¢";
            foreach (var index in candidates)
            {
                result[index] = new BulletRecoveryContext(markerLeft, charWidth, markerText);
            }
        }

        return result;
    }

    private static bool IsMissingBulletCandidate(IReadOnlyList<OcrLine> row, float textLeft, float charWidth)
    {
        if (row.Count != 1 || IsLikelyListMarker(row[0]) || IsPriceText(row[0].Text))
        {
            return false;
        }

        var text = row[0].Text.Trim();
        if (text.Length < 8 || !text.Any(char.IsLetter))
        {
            return false;
        }

        return Math.Abs(Left(row[0].Bounds) - textLeft) <= Math.Max(12, charWidth * 2.25f);
    }

    private static float InferAlignedListTextLeft(IReadOnlyList<IReadOnlyList<OcrLine>> rows)
    {
        var lefts = rows
            .Where(row => row.Count == 1)
            .Where(row => IsMissingBulletCandidate(row, Left(row[0].Bounds), EstimateCharacterWidth(row)))
            .Select(row => Left(row[0].Bounds))
            .Order()
            .ToArray();
        if (lefts.Length < 4)
        {
            return float.NaN;
        }

        var clusters = new List<List<float>>();
        foreach (var left in lefts)
        {
            var cluster = clusters.FirstOrDefault(item => Math.Abs(Median(item) - left) <= 12);
            if (cluster is null)
            {
                clusters.Add([left]);
                continue;
            }

            cluster.Add(left);
        }

        var best = clusters.OrderByDescending(cluster => cluster.Count).First();
        return best.Count >= 4 ? Median(best) : float.NaN;
    }

    private static bool HasRegularVerticalRhythm(IReadOnlyList<IReadOnlyList<OcrLine>> rows)
    {
        if (rows.Count < 4)
        {
            return false;
        }

        var tops = rows.Select(row => row.Min(line => Top(line.Bounds))).Order().ToArray();
        var gaps = tops.Zip(tops.Skip(1), (first, second) => second - first).Where(gap => gap > 0).ToArray();
        if (gaps.Length < 3)
        {
            return false;
        }

        var median = Median(gaps);
        return gaps.Count(gap => Math.Abs(gap - median) <= Math.Max(5, median * 0.3f)) >= gaps.Length - 1;
    }

    private static bool IsExplicitBulletListRow(IReadOnlyList<OcrLine>? row, LayoutContext layout)
    {
        return row is { Count: >= 2 }
            && IsBulletMarkerText(row[0].Text)
            && Math.Abs(Left(row[0].Bounds) - layout.MarkerLeft) <= Math.Max(10, layout.CharWidth * 2)
            && Math.Abs(Left(row[1].Bounds) - layout.TextLeft) <= Math.Max(12, layout.CharWidth * 2.25f);
    }

    private static bool IsBulletMarkerText(string text)
    {
        var trimmed = text.Trim();
        return trimmed is "•" or "·" or "â€¢" or "Â·" or "*";
    }

    private static bool IsLikelyPhoneIconNoiseRow(IReadOnlyList<OcrLine> row)
    {
        if (row.Count != 1)
        {
            return false;
        }

        var text = row[0].Text.Trim();
        var compact = text.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (text.Length is < 4 or > 8 || !text.Contains(' ', StringComparison.Ordinal) || compact.Length < 4)
        {
            return false;
        }

        return compact.Count(character => character is 'o' or 'O' or '0' or 'l' or 'L' or 'I' or '|') >= 3
            && compact.All(character => character is 'o' or 'O' or '0' or 'l' or 'L' or 'I' or '|' or 'D');
    }

    private static string FormatRow(IReadOnlyList<OcrLine> row, OcrCopyMode copyMode, LayoutContext layout)
    {
        var fragments = row
            .Select(line => line with { Text = copyMode == OcrCopyMode.Code ? line.Text : line.Text.Trim() })
            .Where(line => line.Text.Length > 0)
            .OrderBy(line => Left(line.Bounds))
            .ToArray();
        if (fragments.Length == 0)
        {
            return string.Empty;
        }

        var charWidth = EstimateCharacterWidth(fragments);
        var parts = new List<string>();

        parts.Add(fragments[0].Text);
        for (var i = 1; i < fragments.Length; i++)
        {
            var previous = fragments[i - 1];
            var current = fragments[i];
            var gap = Left(current.Bounds) - Right(previous.Bounds);
            var spaceCount = copyMode == OcrCopyMode.Raw && i == 1 && IsLikelyListMarker(previous)
                ? 1
                : InferSpaceCount(gap, charWidth);
            if (copyMode == OcrCopyMode.Raw
                && fragments.Length >= 3
                && spaceCount == 0
                && IsWordLikeFragment(previous.Text)
                && IsWordLikeFragment(current.Text))
            {
                spaceCount = 1;
            }

            if (copyMode == OcrCopyMode.Raw
                && IsPriceText(current.Text)
                && !IsPriceText(previous.Text)
                && spaceCount < 3)
            {
                spaceCount = 3;
            }

            parts.Add(new string(' ', spaceCount));
            parts.Add(current.Text);
        }

        var text = string.Concat(parts);
        return copyMode == OcrCopyMode.Code ? text : NormalizeRawText(text.TrimEnd());
    }

    private static float EstimateCharacterWidth(IReadOnlyList<OcrLine> row)
    {
        var widths = row
            .Select(line =>
            {
                var count = Math.Max(1, line.Text.Count(character => !char.IsWhiteSpace(character)));
                return Width(line.Bounds) / count;
            })
            .Where(width => width > 0)
            .Order()
            .ToArray();
        return widths.Length == 0 ? 8 : widths[widths.Length / 2];
    }

    private static int InferSpaceCount(float gap, float charWidth)
    {
        if (gap <= Math.Max(2, charWidth * 0.35f))
        {
            return 0;
        }

        return Math.Clamp((int)Math.Round(gap / Math.Max(1, charWidth)), 1, 4);
    }

    private static bool SameVisualRow(OcrLine first, OcrLine second)
    {
        var sameColumn = IsSameColumn(first, second);
        var topDifference = Math.Abs(Top(first.Bounds) - Top(second.Bounds));
        if (sameColumn && topDifference > Math.Min(Height(first.Bounds), Height(second.Bounds)) * 0.25f)
        {
            return false;
        }

        var top = Math.Max(Top(first.Bounds), Top(second.Bounds));
        var bottom = Math.Min(Bottom(first.Bounds), Bottom(second.Bounds));
        var overlap = Math.Max(0, bottom - top);
        var minHeight = Math.Min(Math.Max(1, Height(first.Bounds)), Math.Max(1, Height(second.Bounds)));
        if (overlap / minHeight >= 0.55f)
        {
            return true;
        }

        if (sameColumn)
        {
            return false;
        }

        var centerDifference = Math.Abs(CenterY(first.Bounds) - CenterY(second.Bounds));
        var rowTolerance = Math.Max(12, Math.Max(Height(first.Bounds), Height(second.Bounds)));
        return centerDifference <= rowTolerance;
    }

    private static bool IsSameColumn(OcrLine first, OcrLine second)
    {
        return Math.Abs(Left(first.Bounds) - Left(second.Bounds)) <= Math.Max(6, Math.Min(Height(first.Bounds), Height(second.Bounds)) * 0.75f);
    }

    private static bool ShouldInsertBlankLine(IReadOnlyList<OcrLine> previous, IReadOnlyList<OcrLine> current, LayoutContext layout, float medianHeight)
    {
        var previousIsListRow = layout.HasListColumns && IsListRow(previous, layout);
        var currentIsListRow = layout.HasListColumns && IsListRow(current, layout);
        var previousText = FormatRow(previous, OcrCopyMode.Raw, layout);
        var currentText = FormatRow(current, OcrCopyMode.Raw, layout);
        if ((previousIsListRow && currentIsListRow)
            || (StartsNewListItem(previousText) && StartsNewListItem(currentText))
            || (IsLooseBulletText(previousText) && IsLooseBulletText(currentText))
            || (IsHeadingRow(previous) && IsLooseBulletText(currentText)))
        {
            return false;
        }

        if (!previousIsListRow && currentIsListRow && IsHeadingRow(previous))
        {
            return true;
        }

        var previousBottom = previous.Max(line => Bottom(line.Bounds));
        var nextTop = current.Min(line => Top(line.Bounds));
        var gap = nextTop - previousBottom;
        if (IsWrappedTextContinuation(previous, current, gap, medianHeight))
        {
            return false;
        }

        if (previous.Count == 1 && IsHeadingRow(current) && !IsHeadingRow(previous) && !IsShortMetadataRow(current) && gap > Math.Max(6, medianHeight * 0.45f))
        {
            return true;
        }

        if (IsHeadingRow(previous) && !IsHeadingRow(current) && !IsShortMetadataRow(current) && gap > Math.Max(8, medianHeight * 0.65f))
        {
            return true;
        }

        if (gap > Math.Max(14, medianHeight * 1.05f) && EndsProseSentence(previous) && SameParagraphColumn(previous, current))
        {
            return true;
        }

        return gap > Math.Max(20, medianHeight * 1.4f);
    }

    private static bool IsWrappedTextContinuation(IReadOnlyList<OcrLine> previous, IReadOnlyList<OcrLine> current, float gap, float medianHeight)
    {
        if (previous.Count != 1 || current.Count != 1 || gap > Math.Max(28, medianHeight * 2.4f))
        {
            return false;
        }

        var previousText = NormalizeRawText(previous[0].Text.Trim());
        var currentText = NormalizeRawText(current[0].Text.Trim());
        if (previousText.Length == 0 || currentText.Length == 0 || StartsNewListItem(currentText))
        {
            return false;
        }

        var previousLeft = Left(previous[0].Bounds);
        var currentLeft = Left(current[0].Bounds);
        var sameTextColumn = Math.Abs(previousLeft - currentLeft) <= Math.Max(14, EstimateCharacterWidth(previous) * 3);
        var indentedContinuation = currentLeft > previousLeft && currentLeft - previousLeft <= Math.Max(90, Width(previous[0].Bounds) * 0.45f);

        if (StartsNewListItem(previousText))
        {
            return !EndsParagraph(previous) && (sameTextColumn || indentedContinuation);
        }

        if (IsHeadingRow(current))
        {
            return false;
        }

        if (IsHeadingRow(previous))
        {
            return false;
        }

        if (!EndsParagraph(previous) && (sameTextColumn || indentedContinuation))
        {
            return true;
        }

        return false;
    }

    private static bool EndsParagraph(IReadOnlyList<OcrLine> row)
    {
        var text = string.Concat(row.Select(line => line.Text.Trim())).TrimEnd();
        return text.EndsWith(".", StringComparison.Ordinal)
            || text.EndsWith("!", StringComparison.Ordinal)
            || text.EndsWith("?", StringComparison.Ordinal)
            || text.EndsWith(":", StringComparison.Ordinal);
    }

    private static bool EndsProseSentence(IReadOnlyList<OcrLine> row)
    {
        var text = string.Concat(row.Select(line => line.Text.Trim())).TrimEnd();
        return text.EndsWith(".", StringComparison.Ordinal)
            && text.Any(char.IsLower)
            && !LooksLikePriceText(text);
    }

    private static bool SameParagraphColumn(IReadOnlyList<OcrLine> previous, IReadOnlyList<OcrLine> current)
    {
        if (previous.Count != 1 || current.Count != 1)
        {
            return false;
        }

        return Math.Abs(Left(previous[0].Bounds) - Left(current[0].Bounds)) <= Math.Max(16, EstimateCharacterWidth(previous) * 3);
    }

    private static bool StartsNewListItem(string text)
    {
        var trimmed = text.TrimStart();
        var index = 0;
        while (index < trimmed.Length && char.IsDigit(trimmed[index]))
        {
            index++;
        }

        return index > 0
            && index <= 3
            && index < trimmed.Length
            && (trimmed[index] == '.' || trimmed[index] == ')')
            && index + 1 < trimmed.Length
            && char.IsWhiteSpace(trimmed[index + 1]);
    }

    private static bool IsLooseBulletText(string text)
    {
        var normalized = NormalizeBulletText(text).TrimStart();
        return normalized.Length >= 4
            && (normalized.StartsWith("\u2022 ", StringComparison.Ordinal)
                || (normalized[0] is 'o' or 'O' or '0'
                    && char.IsWhiteSpace(normalized[1])
                    && char.IsUpper(normalized[2])
                    && char.IsWhiteSpace(normalized[3])));
    }

    private static string FormatSpatialRows(IReadOnlyList<IReadOnlyList<OcrLine>> rows)
    {
        var allLines = rows.SelectMany(row => row).ToArray();
        var leftOrigin = allLines.Min(line => Left(line.Bounds));
        var charWidth = Math.Max(2, Median(allLines.Select(line => Width(line.Bounds) / Math.Max(1, line.Text.Length))));
        var formatted = new List<string>();
        foreach (var row in rows)
        {
            var parts = new List<string>();
            var cursor = 0;
            foreach (var line in row.OrderBy(line => Left(line.Bounds)))
            {
                var column = Math.Max(0, (int)Math.Round((Left(line.Bounds) - leftOrigin) / charWidth));
                if (column > cursor)
                {
                    parts.Add(new string(' ', Math.Min(40, column - cursor)));
                    cursor = column;
                }

                parts.Add(line.Text);
                cursor += line.Text.Length;
            }

            formatted.Add(string.Concat(parts).TrimEnd());
        }

        return string.Join(Environment.NewLine, formatted.Where(line => line.Length > 0));
    }

    private static bool IsSymbolHeavyCapture(IReadOnlyList<OcrLine> lines)
    {
        var text = string.Concat(lines.Select(line => line.Text));
        if (text.Length < 12)
        {
            return false;
        }

        var visible = text.Count(character => !char.IsWhiteSpace(character));
        if (visible == 0)
        {
            return false;
        }

        var symbols = text.Count(character => !char.IsWhiteSpace(character) && !char.IsLetterOrDigit(character));
        var shortFragments = lines.Count(line => line.Text.Trim().Length is > 0 and <= 4);
        return symbols / (double)visible >= 0.18 && shortFragments >= 3;
    }

    private static string NormalizeRawText(string text)
    {
        text = SplitPackedNavigationText(text);
        text = NormalizeBulletText(text);
        return text
            .Replace(" ,", ",", StringComparison.Ordinal)
            .Replace(" .", ".", StringComparison.Ordinal)
            .Replace(" :", ":", StringComparison.Ordinal)
            .Replace(" ;", ";", StringComparison.Ordinal)
            .Replace(" !", "!", StringComparison.Ordinal)
            .Replace(" ?", "?", StringComparison.Ordinal);
    }

    private static string NormalizeBulletText(string text)
    {
        var bullet = "\u2022";
        var normalized = text
            .Replace("Ã¢â‚¬Â¢", bullet, StringComparison.Ordinal)
            .Replace("â€¢", bullet, StringComparison.Ordinal)
            .Replace("Â·", bullet, StringComparison.Ordinal)
            .Replace("Ã‚Â·", bullet, StringComparison.Ordinal)
            .Replace("·", bullet, StringComparison.Ordinal);

        normalized = normalized
            .Replace(string.Concat(bullet, " ", bullet, " "), string.Concat(bullet, " "), StringComparison.Ordinal)
            .Replace(string.Concat(bullet, bullet, " "), string.Concat(bullet, " "), StringComparison.Ordinal);

        if (normalized.Length >= 4
            && (normalized[0] is 'o' or 'O' or '0')
            && char.IsUpper(normalized[1])
            && char.IsWhiteSpace(normalized[2]))
        {
            return string.Concat("o ", normalized.AsSpan(1));
        }

        return normalized;
    }

    private static string SplitPackedNavigationText(string text)
    {
        if (text.Length < 12)
        {
            return text;
        }

        var segments = text.Split(' ', StringSplitOptions.None);
        if (segments.Length > 1)
        {
            return string.Join(" ", segments.Select(SplitPackedNavigationSegment));
        }

        return SplitPackedNavigationSegment(text);
    }

    private static string SplitPackedNavigationSegment(string text)
    {
        var letterCount = text.Count(char.IsLetter);
        if (letterCount < 10)
        {
            return text;
        }

        var upperAfterLower = 0;
        for (var i = 1; i < text.Length; i++)
        {
            if (IsPackedWordBoundary(text, i))
            {
                upperAfterLower++;
            }
        }

        if (upperAfterLower < 1)
        {
            return text;
        }

        var parts = new List<string>();
        var start = 0;
        for (var i = 1; i < text.Length; i++)
        {
            if (IsPackedWordBoundary(text, i))
            {
                parts.Add(text[start..i]);
                start = i;
            }
        }

        parts.Add(text[start..]);
        return string.Join(" ", parts);
    }

    private static bool IsPackedWordBoundary(string text, int index)
    {
        if (index <= 0 || index >= text.Length)
        {
            return false;
        }

        return (char.IsLower(text[index - 1]) && char.IsUpper(text[index]))
            || (text[index - 1] == '.'
                && char.IsUpper(text[index])
                && index + 1 < text.Length
                && char.IsLower(text[index + 1]));
    }

    private static bool IsAllCapsText(string text)
    {
        var letters = text.Where(char.IsLetter).ToArray();
        return letters.Length >= 3 && letters.All(character => !char.IsLower(character));
    }

    private static bool IsAllCapsSectionFragment(OcrLine line)
    {
        var text = line.Text.Trim();
        return text.Length is >= 3 and <= 32
            && !LooksLikePriceText(text)
            && IsAllCapsText(text);
    }

    private static bool IsWordLikeFragment(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length > 0
            && trimmed.Any(char.IsLetterOrDigit)
            && trimmed.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '&' or '\'' or '/');
    }

    private static bool IsListRow(IReadOnlyList<OcrLine> row, LayoutContext layout)
    {
        if (!layout.HasListColumns || row.Count == 0)
        {
            return false;
        }

        if (row.Count >= 2 && IsLikelyListMarker(row[0]))
        {
            return true;
        }

        if (row.Count == 1)
        {
            var left = Left(row[0].Bounds);
            return Math.Abs(left - layout.TextLeft) <= Math.Max(6, layout.CharWidth * 1.5f);
        }

        return false;
    }

    private static bool IsHeadingRow(IReadOnlyList<OcrLine> row)
    {
        if (row.Count != 1)
        {
            return false;
        }

        var text = row[0].Text.Trim();
        return text.Length is > 0 and <= 80
            && !text.Contains('$', StringComparison.Ordinal)
            && !text.Contains(" - ", StringComparison.Ordinal)
            && !text.EndsWith(".", StringComparison.Ordinal)
            && !text.EndsWith(",", StringComparison.Ordinal);
    }

    private static bool IsLikelyListMarker(OcrLine line)
    {
        var text = line.Text.Trim();
        if (text.Length == 0 || text.Length > 4)
        {
            return false;
        }

        if (IsBulletMarkerText(text))
        {
            return true;
        }

        return text.All(character => char.IsDigit(character) || character is '#' or '-' or '*' or '•' or '·');
    }

    private static float Left(OcrQuadrilateral box) => new[] { box.TopLeft.X, box.TopRight.X, box.BottomRight.X, box.BottomLeft.X }.Min();

    private static float Right(OcrQuadrilateral box) => new[] { box.TopLeft.X, box.TopRight.X, box.BottomRight.X, box.BottomLeft.X }.Max();

    private static float Top(OcrQuadrilateral box) => new[] { box.TopLeft.Y, box.TopRight.Y, box.BottomRight.Y, box.BottomLeft.Y }.Min();

    private static float Bottom(OcrQuadrilateral box) => new[] { box.TopLeft.Y, box.TopRight.Y, box.BottomRight.Y, box.BottomLeft.Y }.Max();

    private static float CenterY(OcrQuadrilateral box) => (Top(box) + Bottom(box)) / 2;

    private static float HorizontalGap(OcrLine first, OcrLine second) => Math.Max(Left(first.Bounds), Left(second.Bounds)) - Math.Min(Right(first.Bounds), Right(second.Bounds));

    private static float Width(OcrQuadrilateral box) => Right(box) - Left(box);

    private static float Height(OcrQuadrilateral box) => Bottom(box) - Top(box);

    private static float Median(IEnumerable<float> values)
    {
        var ordered = values.Order().ToArray();
        return ordered.Length == 0 ? 0 : ordered[ordered.Length / 2];
    }

    private sealed record ColumnCandidate(float Left, string Text, float Width);

    private sealed record LayoutContext(bool HasListColumns, float MarkerLeft, float TextLeft, float CharWidth, string MarkerText);

    private sealed record BulletRecoveryContext(float MarkerLeft, float CharWidth, string MarkerText);
}
