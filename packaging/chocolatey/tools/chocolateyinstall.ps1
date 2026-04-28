$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.25.1-slskdn.185/slskdn-main-win-x64.zip"
$checksum   = "585b4e1807db552b66265327971ceda8ac6222ca0aab7dca3c862788c0ccd34f"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
