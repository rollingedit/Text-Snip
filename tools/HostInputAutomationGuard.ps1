function Test-HostInputAutomationAllowed {
    param(
        [bool]$AllowedBySwitch = $false
    )

    return $AllowedBySwitch -or $env:OCRSNIP_ALLOW_HOST_INPUT_AUTOMATION -eq "1"
}

function Assert-HostInputAutomationAllowed {
    param(
        [bool]$AllowedBySwitch = $false,
        [string]$Reason = "This validation sends synthetic keyboard or mouse input to the active desktop."
    )

    if (Test-HostInputAutomationAllowed -AllowedBySwitch $AllowedBySwitch) {
        return
    }

    throw @"
Host input automation is blocked by default.

$Reason

Run this only inside a disposable VM or a dedicated validation machine. To unlock it for that session, either pass -AllowHostInputAutomation or set:
  `$env:OCRSNIP_ALLOW_HOST_INPUT_AUTOMATION = "1"
"@
}
