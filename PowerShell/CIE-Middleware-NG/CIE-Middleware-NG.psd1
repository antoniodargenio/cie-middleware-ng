@{
    RootModule           = 'CIE-Middleware-NG.psm1'
    ModuleVersion        = '2.0.0.45'
    GUID                 = '6b0f6d0e-3a52-4c0e-9f3e-5a0d6c1f7e21'
    Author               = 'CIE Middleware NG'
    CompanyName          = 'CIE Middleware NG'
    Description          = 'Abbinamento, PIN, firma e verifica con la CIE da riga di comando (usa CIEPKI-NG.dll).'
    PowerShellVersion    = '5.1'
    ProcessorArchitecture = 'Amd64'
    FunctionsToExport    = @('Get-Cie', 'Register-Cie', 'Unregister-Cie', 'Set-CiePin', 'Unlock-CiePin', 'Add-CieSignature', 'Test-CieSignature', 'Export-CieSignedContent')
    CmdletsToExport      = @()
    VariablesToExport    = @()
    AliasesToExport      = @()
    PrivateData          = @{
        PSData = @{
            Tags       = @('CIE', 'smartcard', 'firma', 'PAdES', 'CAdES')
            ProjectUri = 'https://github.com/antoniodargenio/cie-middleware-ng'
        }
    }
}
