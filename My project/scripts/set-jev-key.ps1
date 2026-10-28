# Run locally when you have a key. It is never written into this project or command history.
$secureKey = Read-Host 'TypeSafe API key (input nascosto)' -AsSecureString
$keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try {
    $keyValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    if ([string]::IsNullOrWhiteSpace($keyValue)) { throw 'Chiave vuota.' }
    [Environment]::SetEnvironmentVariable('TYPESAFE_API_KEY', $keyValue.Trim(), 'User')
    Write-Host 'TYPESAFE_API_KEY configurata per questo utente. In Unity premi 2 per JEV.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)
    $keyValue = $null
    $secureKey.Dispose()
}

