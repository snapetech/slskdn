$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.119/slskdn-main-win-x64.zip"
$checksum   = "9adfdd79646eeeaaa2454db44ab05dc46670a65884013922f7ab08c5fa91ca0d"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
