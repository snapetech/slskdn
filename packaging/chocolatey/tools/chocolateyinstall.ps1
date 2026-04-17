$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.131/slskdn-main-win-x64.zip"
$checksum   = "0ea19eb673164d2468a5627911e513029fb7df795e53f4108ff2392c2ed8dda6"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
