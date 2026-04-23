$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url        = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.177/slskdn-main-win-x64.zip"
$checksum   = "258fc95035ea4905ae90432ed396af272448773b56e5d23fcdba638fa75199b6"

Install-ChocolateyZipPackage -PackageName 'slskdn' -Url $url -UnzipLocation $toolsDir -Checksum $checksum -ChecksumType 'sha256'
