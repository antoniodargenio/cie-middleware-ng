<#
.SYNOPSIS
    Modulo PowerShell per CIE Middleware NG: le stesse operazioni dell'applicazione CIE-Middleware-NG, da riga di comando.
.DESCRIPTION
    Richiama direttamente CIEPKI-NG.dll (installata in System32 dal setup di CIE Middleware NG), quindi
    richiede Windows a 64 bit, un processo PowerShell a 64 bit e un lettore di smart card con la CIE inserita.
    Funzioni: elenco/abbinamento/disabilitazione delle CIE, cambio PIN, sblocco con PUK,
    firma PAdES (PDF) e CAdES (.p7m), verifica di file firmati, estrazione del contenuto di un .p7m.
    Per l'aiuto di ogni comando: Get-Help <comando> -Full
#>

Set-StrictMode -Version 2.0

# ---------------------------------------------------------------------------------------------
# Strato nativo: P/Invoke verso CIEPKI-NG.dll. Le operazioni lente (abbinamento, cambio PIN,
# sblocco, firma) girano su un thread dedicato, cosi' PowerShell puo' mostrare l'avanzamento
# mentre la libreria nativa richiama le callback (che non possono eseguire script PowerShell).
# ---------------------------------------------------------------------------------------------
if (-not ('CieMiddlewareNg.CieOperation' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace CieMiddlewareNg
{
    // Callback della libreria nativa: avanzamento, fine abbinamento, fine firma
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int ProgressCallback(int progress, string message);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int CompletedCallback(string pan, string name, string efSeriale);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int SignCompletedCallback(int retValue);

    // Esito della verifica di un firmatario: deve corrispondere alla struct nativa
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct VerifyInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string surname;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string cn;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string signingTime;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string cadn;
        public int CertRevocStatus;
        [MarshalAs(UnmanagedType.U1)] public bool isSignValid;
        [MarshalAs(UnmanagedType.U1)] public bool isCertValid;
    }

    internal static class Native
    {
        [DllImport("CIEPKI-NG.dll")]
        public static extern int VerificaCIEAbilitata(string pan);

        [DllImport("CIEPKI-NG.dll")]
        public static extern int DisabilitaCIE(string pan);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int AbbinaCIE(string pan, string pin, int[] attempts, ProgressCallback progress, CompletedCallback completed);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int ChangePIN(string pin, string newPin, int[] attempts, ProgressCallback progress);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int UnlockPIN(string puk, string newPin, int[] attempts, ProgressCallback progress);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int firmaConCIE(string inFilePath, string type, string pin, string pan, int page, float x, float y, float w, float h, string imagePathFile, string outFilePath, ProgressCallback progress, SignCompletedCallback completed, string tsaUrl, string tsaUsername, string tsaPassword);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int estraiP7m(string inFilePath, string outFilePath);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int verificaConCIE(string filePath, string proxyAddress, int proxyPort, string usrPass);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int getVerifyInfo(int index, ref VerifyInfo info);
    }

    // Operazione lunga eseguita su un thread dedicato; PowerShell ne legge lo stato.
    public class CieOperation
    {
        public ConcurrentQueue<string> Messages = new ConcurrentQueue<string>();
        public volatile int Progress;
        public volatile string LastMessage = "";
        public volatile bool Done;
        public int Result = -1;          // codice restituito dalla libreria
        public string Error;             // eccezione managed (es. DLL non trovata)
        public int[] Attempts = new int[1];
        public string Pan, Owner, Serial; // valorizzati dall'abbinamento

        private ProgressCallback progressCb;
        private CompletedCallback completedCb;
        private SignCompletedCallback signCb;

        private int OnProgress(int progress, string message)
        {
            Progress = progress;
            LastMessage = message ?? "";
            Messages.Enqueue(progress + "% " + LastMessage);
            return 0;
        }

        private void Run(Func<int> f)
        {
            Thread th = new Thread(delegate ()
            {
                try { Result = f(); }
                catch (Exception e) { Error = e.Message; }
                finally { Done = true; }
            });
            th.IsBackground = true;
            th.Start();
        }

        public static CieOperation Pair(string pin)
        {
            CieOperation op = new CieOperation();
            op.progressCb = op.OnProgress;
            op.completedCb = delegate (string pan, string name, string serial)
            {
                op.Pan = pan; op.Owner = name; op.Serial = serial; return 0;
            };
            op.Run(delegate () { return Native.AbbinaCIE(null, pin, op.Attempts, op.progressCb, op.completedCb); });
            return op;
        }

        public static CieOperation ChangePin(string pin, string newPin)
        {
            CieOperation op = new CieOperation();
            op.progressCb = op.OnProgress;
            op.Run(delegate () { return Native.ChangePIN(pin, newPin, op.Attempts, op.progressCb); });
            return op;
        }

        public static CieOperation UnlockPin(string puk, string newPin)
        {
            CieOperation op = new CieOperation();
            op.progressCb = op.OnProgress;
            op.Run(delegate () { return Native.UnlockPIN(puk, newPin, op.Attempts, op.progressCb); });
            return op;
        }

        public static CieOperation Sign(string inFile, string type, string pin, string pan, int page, float x, float y, float w, float h,
                                        string image, string outFile, string tsaUrl, string tsaUser, string tsaPass)
        {
            CieOperation op = new CieOperation();
            op.progressCb = op.OnProgress;
            op.signCb = delegate (int ret) { return 0; };
            op.Run(delegate ()
            {
                return Native.firmaConCIE(inFile, type, pin, pan, page, x, y, w, h, image, outFile, op.progressCb, op.signCb, tsaUrl, tsaUser, tsaPass);
            });
            return op;
        }
    }

    // Operazioni rapide, eseguite in modo sincrono
    public static class CieQuick
    {
        public static int IsPaired(string pan) { return Native.VerificaCIEAbilitata(pan); }
        public static int Unpair(string pan) { return Native.DisabilitaCIE(pan); }
        public static int ExtractP7m(string inFile, string outFile) { return Native.estraiP7m(inFile, outFile); }

        // Verifica un file firmato: restituisce il numero di firme (o un codice di errore <= 0) e gli esiti
        public static int Verify(string path, string proxy, int proxyPort, string credentials, out VerifyInfo[] infos)
        {
            infos = new VerifyInfo[0];
            int n = Native.verificaConCIE(path, proxy, proxyPort, credentials);
            if (n <= 0) return n;
            infos = new VerifyInfo[n];
            for (int i = 0; i < n; i++) Native.getVerifyInfo(i, ref infos[i]);
            return n;
        }
    }
}
'@
}

# ---------------------------------------------------------------------------------------------
# Funzioni di supporto (non esportate)
# ---------------------------------------------------------------------------------------------

# File con le CIE abbinate tramite questo modulo (come l'app, tiene anche nome e seriale)
$script:StorePath = Join-Path $env:LOCALAPPDATA 'CIE-Middleware-NG\cie-powershell.json'
# Marca temporale usata di default (stessa dell'applicazione)
$script:DefaultTsaUrl = 'http://timestamp.actalis.it'

function Assert-CieEnvironment {
    if (-not [Environment]::Is64BitProcess) {
        throw 'CIEPKI-NG.dll è a 64 bit: usare un PowerShell a 64 bit.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $env:SystemRoot 'System32\CIEPKI-NG.dll'))) {
        throw 'CIEPKI-NG.dll non trovata in System32: installare CIE Middleware NG.'
    }
}

# Messaggio in italiano per un codice di ritorno della libreria (stessi esiti mostrati dall'app)
function Get-CieErrorMessage {
    param([int]$Code, [int]$Attempts)
    switch ($Code) {
        0x01 { 'Operazione annullata.' }
        0x05 { 'Errore inaspettato durante la comunicazione con la smart card.' }
        0x30 { 'Errore del lettore di smart card.' }
        0xA0 { "Il PIN digitato è errato. Rimangono $Attempts tentativi." }
        0xA1 { 'PIN non valido.' }
        0xA2 { 'Il PIN deve essere composto da cifre numeriche.' }
        0xA3 { 'PIN scaduto.' }
        0xA4 { 'Carta bloccata: munirsi del codice PUK e usare Unlock-CiePin.' }
        0xE0 { 'CIE non presente sul lettore.' }
        0xE1 { 'CIE non presente sul lettore (carta non riconosciuta).' }
        0xF0 { 'Carta già abilitata.' }
        0xF1 { 'La CIE presente sul lettore è diversa da quella indicata (PAN non corrispondente).' }
        0xF2 { 'Marca temporale non ottenuta: riprovare con -TimestampUrl diverso oppure con -NoTimestamp.' }
        default { ('Errore 0x{0:X}.' -f $Code) }
    }
}

# Solleva un errore terminante con il messaggio corrispondente al codice
function Stop-CieError {
    param($Cmdlet, [int]$Code, [int]$Attempts = 0, [string]$Detail)
    $msg = if ($Detail) { $Detail } else { Get-CieErrorMessage -Code $Code -Attempts $Attempts }
    $rec = [System.Management.Automation.ErrorRecord]::new([System.Exception]::new($msg), ('CIE_0x{0:X}' -f $Code), 'NotSpecified', $null)
    $Cmdlet.ThrowTerminatingError($rec)
}

# Attende un'operazione su thread dedicato mostrando l'avanzamento
function Wait-CieOperation {
    param($Operation, [string]$Activity)
    $m = $null
    while (-not $Operation.Done) {
        while ($Operation.Messages.TryDequeue([ref]$m)) { Write-Verbose $m }
        Write-Progress -Activity $Activity -Status $Operation.LastMessage -PercentComplete ([Math]::Max(0, [Math]::Min(100, $Operation.Progress)))
        Start-Sleep -Milliseconds 100
    }
    while ($Operation.Messages.TryDequeue([ref]$m)) { Write-Verbose $m }
    Write-Progress -Activity $Activity -Completed
}

# SecureString -> stringa (solo per il passaggio alla libreria nativa)
function ConvertFrom-CieSecureString {
    param([System.Security.SecureString]$Secure)
    $ptr = [System.Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($Secure)
    try { [System.Runtime.InteropServices.Marshal]::PtrToStringUni($ptr) }
    finally { [System.Runtime.InteropServices.Marshal]::ZeroFreeGlobalAllocUnicode($ptr) }
}

# Ottiene il PIN: dal parametro oppure chiedendolo (senza eco)
function Resolve-CieSecret {
    param([System.Security.SecureString]$Value, [string]$Prompt)
    if ($null -eq $Value) { $Value = Read-Host -Prompt $Prompt -AsSecureString }
    ConvertFrom-CieSecureString $Value
}

# Il nuovo PIN non puo' avere cifre tutte uguali ne' sequenze crescenti/decrescenti (stesse regole dell'app)
function Test-CieNewPinRules {
    param([string]$NewPin)
    if ($NewPin -notmatch '^\d{8}$') { return 'Il nuovo PIN deve essere composto da 8 numeri.' }
    $d = [int[]][char[]]$NewPin
    $same = $true; $asc = $true; $desc = $true
    for ($i = 1; $i -lt $d.Length; $i++) {
        if ($d[$i] -ne $d[0]) { $same = $false }
        if ($d[$i] -ne $d[$i - 1] + 1) { $asc = $false }
        if ($d[$i] -ne $d[$i - 1] - 1) { $desc = $false }
    }
    if ($same -or $asc -or $desc) { return 'Il nuovo PIN non può avere cifre tutte uguali o consecutive.' }
    return $null
}

function Read-CieStore {
    if (-not (Test-Path -LiteralPath $script:StorePath)) { return @() }
    try { @(Get-Content -LiteralPath $script:StorePath -Raw -Encoding UTF8 | ConvertFrom-Json) } catch { @() }
}

function Save-CieStore {
    param($Entries)
    $dir = Split-Path $script:StorePath
    if (-not (Test-Path -LiteralPath $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force }
    ConvertTo-Json -InputObject @($Entries) | Set-Content -LiteralPath $script:StorePath -Encoding UTF8
}

# CIE abbinate tramite l'applicazione grafica (sola lettura del suo user.config piu' recente)
function Read-CieAppList {
    $root = Join-Path $env:LOCALAPPDATA 'CIE-Middleware-NG'
    if (-not (Test-Path -LiteralPath $root)) { return @() }
    $cfg = Get-ChildItem -LiteralPath $root -Recurse -Filter user.config -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if (-not $cfg) { return @() }
    try {
        $node = ([xml](Get-Content -LiteralPath $cfg.FullName -Raw)).SelectSingleNode("//setting[@name='cieList']/value")
        if (-not $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) { return @() }
        $json = $node.InnerText | ConvertFrom-Json
        foreach ($p in $json.PSObject.Properties) {
            [pscustomobject]@{ Pan = $p.Value.Pan; Owner = $p.Value.Owner; SerialNumber = $p.Value.SerialNumber }
        }
    } catch { @() }
}

# ---------------------------------------------------------------------------------------------
# Comandi esportati
# ---------------------------------------------------------------------------------------------

function Get-Cie {
<#
.SYNOPSIS
    Elenca le CIE abbinate (tramite questo modulo o tramite l'applicazione).
.DESCRIPTION
    Per ogni CIE mostra PAN, intestatario e numero seriale; la colonna Abbinata conferma, interrogando
    la libreria, che l'abbinamento è ancora attivo. Il PAN serve a Add-CieSignature e Unregister-Cie.
.EXAMPLE
    Get-Cie
#>
    [CmdletBinding()]
    param([string]$Pan)
    Assert-CieEnvironment
    $seen = @{}
    $items = foreach ($e in @(Read-CieStore) + @(Read-CieAppList)) {
        if (-not $e -or -not $e.Pan -or $seen.ContainsKey($e.Pan)) { continue }
        $seen[$e.Pan] = $true
        if ($Pan -and $e.Pan -ne $Pan) { continue }
        [pscustomobject]@{
            PSTypeName   = 'CieMiddlewareNg.Cie'
            Pan          = $e.Pan
            Owner        = $e.Owner
            SerialNumber = $e.SerialNumber
            Abbinata     = ([CieMiddlewareNg.CieQuick]::IsPaired($e.Pan) -eq 1)
        }
    }
    $items
}

function Register-Cie {
<#
.SYNOPSIS
    Abbina la CIE presente sul lettore (equivalente della scheda "Abbina CIE").
.DESCRIPTION
    Richiede il PIN completo di 8 cifre; se omesso viene chiesto senza eco. Restituisce l'oggetto CIE abbinata.
.EXAMPLE
    Register-Cie
#>
    [CmdletBinding()]
    param([System.Security.SecureString]$Pin)
    Assert-CieEnvironment
    $plain = Resolve-CieSecret $Pin 'PIN della CIE (8 cifre)'
    if ($plain -notmatch '^\d{8}$') { Stop-CieError $PSCmdlet 0xA2 -Detail 'Il PIN deve essere composto da 8 numeri.' }
    $op = [CieMiddlewareNg.CieOperation]::Pair($plain)
    Wait-CieOperation $op 'Abbinamento CIE'
    $plain = $null
    if ($op.Error) { Stop-CieError $PSCmdlet -1 -Detail $op.Error }
    if ($op.Result -ne 0) { Stop-CieError $PSCmdlet $op.Result $op.Attempts[0] }

    $entries = @(Read-CieStore | Where-Object { $_.Pan -ne $op.Pan })
    $entries += [pscustomobject]@{ Pan = $op.Pan; Owner = $op.Owner; SerialNumber = $op.Serial }
    Save-CieStore $entries
    Get-Cie -Pan $op.Pan
}

function Unregister-Cie {
<#
.SYNOPSIS
    Disabilita una CIE abbinata (equivalente di "Rimuovi CIE").
.DESCRIPTION
    Elimina l'abbinamento nella libreria. Se la CIE era stata abbinata con l'applicazione grafica,
    resta nell'elenco dell'applicazione finché non viene rimossa anche da lì.
.EXAMPLE
    Get-Cie | Unregister-Cie
#>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param([Parameter(Mandatory, ValueFromPipelineByPropertyName)][string]$Pan)
    begin { Assert-CieEnvironment }
    process {
        if (-not $PSCmdlet.ShouldProcess("CIE $Pan", 'Disabilita abbinamento')) { return }
        $rc = [CieMiddlewareNg.CieQuick]::Unpair($Pan)
        if ($rc -ne 0) { Stop-CieError $PSCmdlet $rc -Detail "Impossibile disabilitare la CIE $Pan (non risulta abbinata)." }
        Save-CieStore @(Read-CieStore | Where-Object { $_.Pan -ne $Pan })
        Write-Verbose "CIE $Pan disabilitata."
    }
}

function Set-CiePin {
<#
.SYNOPSIS
    Cambia il PIN della CIE presente sul lettore.
.DESCRIPTION
    PIN attuale e nuovo PIN sono di 8 cifre; il nuovo non può avere cifre tutte uguali o consecutive.
    Se omessi vengono chiesti senza eco.
.EXAMPLE
    Set-CiePin
#>
    [CmdletBinding(SupportsShouldProcess)]
    param([System.Security.SecureString]$Pin, [System.Security.SecureString]$NewPin)
    Assert-CieEnvironment
    $old = Resolve-CieSecret $Pin 'PIN attuale (8 cifre)'
    $new = Resolve-CieSecret $NewPin 'Nuovo PIN (8 cifre)'
    if ($old -notmatch '^\d{8}$') { Stop-CieError $PSCmdlet 0xA2 -Detail 'Il PIN attuale deve essere composto da 8 numeri.' }
    $rule = Test-CieNewPinRules $new
    if ($rule) { Stop-CieError $PSCmdlet 0xA1 -Detail $rule }
    if (-not $PSCmdlet.ShouldProcess('CIE sul lettore', 'Cambia PIN')) { return }
    $op = [CieMiddlewareNg.CieOperation]::ChangePin($old, $new)
    Wait-CieOperation $op 'Cambio PIN'
    $old = $new = $null
    if ($op.Error) { Stop-CieError $PSCmdlet -1 -Detail $op.Error }
    if ($op.Result -ne 0) { Stop-CieError $PSCmdlet $op.Result $op.Attempts[0] }
    Write-Verbose 'Il PIN è stato modificato con successo.'
}

function Unlock-CiePin {
<#
.SYNOPSIS
    Sblocca la CIE con il PUK impostando un nuovo PIN.
.EXAMPLE
    Unlock-CiePin
#>
    [CmdletBinding(SupportsShouldProcess)]
    param([System.Security.SecureString]$Puk, [System.Security.SecureString]$NewPin)
    Assert-CieEnvironment
    $puk8 = Resolve-CieSecret $Puk 'PUK (8 cifre)'
    $new = Resolve-CieSecret $NewPin 'Nuovo PIN (8 cifre)'
    if ($puk8 -notmatch '^\d{8}$') { Stop-CieError $PSCmdlet 0xA2 -Detail 'Il PUK deve essere composto da 8 numeri.' }
    $rule = Test-CieNewPinRules $new
    if ($rule) { Stop-CieError $PSCmdlet 0xA1 -Detail $rule }
    if (-not $PSCmdlet.ShouldProcess('CIE sul lettore', 'Sblocca con PUK')) { return }
    $op = [CieMiddlewareNg.CieOperation]::UnlockPin($puk8, $new)
    Wait-CieOperation $op 'Sblocco CIE'
    $puk8 = $new = $null
    if ($op.Error) { Stop-CieError $PSCmdlet -1 -Detail $op.Error }
    if ($op.Result -ne 0) { Stop-CieError $PSCmdlet $op.Result $op.Attempts[0] }
    Write-Verbose 'Carta sbloccata con successo.'
}

function Add-CieSignature {
<#
.SYNOPSIS
    Firma un file con la CIE (PAdES per i PDF, CAdES/.p7m per qualsiasi file).
.DESCRIPTION
    Usa le ultime 4 cifre del PIN (si può indicare il PIN intero o le sole 4 cifre). La marca temporale
    è richiesta di default (server Actalis come l'applicazione): -NoTimestamp la disattiva,
    -TimestampUrl indica un altro server (con -TimestampCredential se serve l'autenticazione).
    Per la firma grafica PAdES indicare -SignatureImage: X, Y, Width e Height sono frazioni della pagina
    (0-1) con origine in alto a sinistra; Y è il bordo inferiore dell'immagine; Page parte da 1.
.EXAMPLE
    Get-Cie | Select-Object -First 1 | Add-CieSignature -Path .\contratto.pdf
.EXAMPLE
    Add-CieSignature -Path .\atto.docx -Format Cades -Pan 198654729236 -NoTimestamp
#>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][string]$Path,
        [Parameter(ValueFromPipelineByPropertyName)][string]$Pan,
        [ValidateSet('Pades', 'Cades')][string]$Format,
        [string]$OutFile,
        [System.Security.SecureString]$Pin,
        [switch]$NoTimestamp,
        [string]$TimestampUrl,
        [pscredential]$TimestampCredential,
        [string]$SignatureImage,
        [ValidateRange(1, 10000)][int]$Page = 1,
        [ValidateRange(0.0, 1.0)][float]$X = 0.55,
        [ValidateRange(0.0, 1.0)][float]$Y = 0.95,
        [ValidateRange(0.0, 1.0)][float]$Width = 0.40,
        [ValidateRange(0.0, 1.0)][float]$Height = 0.10,
        [switch]$Force
    )
    begin { Assert-CieEnvironment }
    process {
        $in = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
        if (-not $Format) { $Format = if ([IO.Path]::GetExtension($in) -ieq '.pdf') { 'Pades' } else { 'Cades' } }
        if ($Format -eq 'Pades' -and [IO.Path]::GetExtension($in) -ine '.pdf') { throw 'La firma PAdES richiede un file PDF: usare -Format Cades.' }
        if ($SignatureImage -and $Format -ne 'Pades') { throw '-SignatureImage è valido solo per la firma PAdES.' }

        # CIE da usare: quella indicata, altrimenti l'unica abbinata
        if (-not $Pan) {
            $all = @(Get-Cie | Where-Object Abbinata)
            if ($all.Count -eq 1) { $Pan = $all[0].Pan }
            elseif ($all.Count -eq 0) { throw 'Nessuna CIE abbinata: usare Register-Cie.' }
            else { throw 'Più CIE abbinate: indicare -Pan (vedi Get-Cie).' }
        }

        if (-not $OutFile) {
            $dir = [IO.Path]::GetDirectoryName($in); $base = [IO.Path]::GetFileNameWithoutExtension($in)
            $OutFile = if ($Format -eq 'Pades') { Join-Path $dir "$base-signed.pdf" } else { "$in.p7m" }
        }
        $out = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutFile)
        if ((Test-Path -LiteralPath $out) -and -not $Force) { throw "Il file di destinazione esiste già: $out (usare -Force per sovrascriverlo)." }

        # Parametri di firma grafica (pagina a base 0, come si aspetta la libreria)
        $img = $null; $pg = 0; $fx = 0.0; $fy = 0.0; $fw = 0.0; $fh = 0.0
        if ($SignatureImage) {
            $img = (Resolve-Path -LiteralPath $SignatureImage -ErrorAction Stop).ProviderPath
            $pg = $Page - 1; $fx = $X; $fy = $Y; $fw = $Width; $fh = $Height
        }

        # Marca temporale
        $tsaUrl = $null; $tsaUser = $null; $tsaPass = $null
        if (-not $NoTimestamp) {
            $tsaUrl = if ($TimestampUrl) { $TimestampUrl } else { $script:DefaultTsaUrl }
            if ($TimestampCredential) { $tsaUser = $TimestampCredential.UserName; $tsaPass = $TimestampCredential.GetNetworkCredential().Password }
        }

        $plain = Resolve-CieSecret $Pin 'PIN della CIE (ultime 4 cifre)'
        if ($plain -notmatch '^\d{4,8}$') { Stop-CieError $PSCmdlet 0xA2 -Detail 'Il PIN deve essere composto da cifre numeriche (le ultime 4 cifre).' }
        $plain = $plain.Substring($plain.Length - 4)

        if (-not $PSCmdlet.ShouldProcess($in, "Firma $Format con CIE $Pan")) { return }
        $type = if ($Format -eq 'Pades') { 'pdf' } else { 'p7m' }
        $op = [CieMiddlewareNg.CieOperation]::Sign($in, $type, $plain, $Pan, $pg, $fx, $fy, $fw, $fh, $img, $out, $tsaUrl, $tsaUser, $tsaPass)
        Wait-CieOperation $op 'Firma con CIE'
        $plain = $null; $tsaPass = $null
        if ($op.Error) { Stop-CieError $PSCmdlet -1 -Detail $op.Error }
        if ($op.Result -ne 0) { Stop-CieError $PSCmdlet $op.Result $op.Attempts[0] }
        Get-Item -LiteralPath $out
    }
}

function Test-CieSignature {
<#
.SYNOPSIS
    Verifica le firme di un file PAdES/CAdES (equivalente della scheda "Verifica firma").
.DESCRIPTION
    Restituisce un oggetto per ogni firmatario con validità di firma e certificato e stato di revoca.
    Non serve il lettore di smart card. -ProxyUrl/-ProxyPort/-ProxyCredential per accedere alla rete tramite proxy.
.EXAMPLE
    Test-CieSignature .\contratto-signed.pdf | Format-List
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromPipelineByPropertyName)][Alias('FullName')][string]$Path,
        [string]$ProxyUrl,
        [int]$ProxyPort = 0,
        [pscredential]$ProxyCredential
    )
    begin { Assert-CieEnvironment }
    process {
        $in = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
        $proxy = $null; $port = -1; $cred = $null
        if ($ProxyUrl) {
            $proxy = $ProxyUrl; $port = $ProxyPort
            if ($ProxyCredential) { $cred = '{0}:{1}' -f $ProxyCredential.UserName, $ProxyCredential.GetNetworkCredential().Password }
        }
        $infos = $null
        $n = [CieMiddlewareNg.CieQuick]::Verify($in, $proxy, $port, $cred, [ref]$infos)
        if ($n -le 0) { Stop-CieError $PSCmdlet $n -Detail "Nessuna firma verificabile in $in (codice $n)." }
        $revoc = @('Valido', 'Revocato', 'Sospeso', 'Sconosciuto')
        $i = 0
        foreach ($v in $infos) {
            $i++
            [pscustomobject]@{
                PSTypeName      = 'CieMiddlewareNg.Signature'
                File            = $in
                Firmatario      = $i
                Nome            = ('{0} {1}' -f $v.name, $v.surname).Trim()
                CodiceFiscale   = $v.cn
                DataFirma       = $v.signingTime
                FirmaValida     = $v.isSignValid
                CertificatoOk   = $v.isCertValid
                Revoca          = if ($v.CertRevocStatus -ge 0 -and $v.CertRevocStatus -lt $revoc.Count) { $revoc[$v.CertRevocStatus] } else { "Codice $($v.CertRevocStatus)" }
                EnteEmittente   = $v.cadn
            }
        }
    }
}

function Export-CieSignedContent {
<#
.SYNOPSIS
    Estrae il documento originale da un file firmato .p7m.
.EXAMPLE
    Export-CieSignedContent .\atto.docx.p7m -OutFile .\atto.docx
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)][string]$Path,
        [string]$OutFile,
        [switch]$Force
    )
    Assert-CieEnvironment
    $in = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
    if (-not $OutFile) {
        # Stesso criterio dell'app: tolto .p7m e i marcatori di firma
        $name = [IO.Path]::GetFileNameWithoutExtension($in)
        if ($name.StartsWith('signed-')) { $name = $name.Substring(7) }
        $name = $name.Replace('-signed', '')
        $OutFile = Join-Path ([IO.Path]::GetDirectoryName($in)) $name
    }
    $out = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutFile)
    if ((Test-Path -LiteralPath $out) -and -not $Force) { throw "Il file di destinazione esiste già: $out (usare -Force per sovrascriverlo)." }
    $rc = [CieMiddlewareNg.CieQuick]::ExtractP7m($in, $out)
    if ($rc -ne 0) { Stop-CieError $PSCmdlet $rc -Detail 'Impossibile estrarre il file.' }
    Get-Item -LiteralPath $out
}

Export-ModuleMember -Function Get-Cie, Register-Cie, Unregister-Cie, Set-CiePin, Unlock-CiePin, Add-CieSignature, Test-CieSignature, Export-CieSignedContent
