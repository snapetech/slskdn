$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.111/slskdn-main-win-x64.zip"
$checksum   = "df7b4b1afeaf93c0631287f51722279eaa9782f2bfe15d853dbba022a8756e5a"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
