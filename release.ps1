# This script will create a new version of all stackray packages or for just one package, providing the version as parameter or the package name and the version
# NOTE: the script will go to `master` if not already there.

$all_packages=$true
$version=$args[0]
if (!$version){
    echo "You must supply a version for all packages or a package name and a version"
    return
}
$packages="com.stackray.renderer",
          "com.stackray.sprite",
          "com.stackray.entities",
          "com.stackray.mathematics",
          "com.stackray.collections",
          "com.stackray.jobs",
          "com.stackray.burst",
          "com.stackray.transforms",
          "com.stackray.text"

if ($args.Count -eq 2){
    $packages=($args[0])
    $version=$args[1]
    $all_packages=$false
}
$deploy_dir="Deploy"     
if (!$(Test-Path -Path $deploy_dir)){
  mkdir $deploy_dir
  git clone git@github.com:GilbertoGojira/DOTS-Stackray.git $deploy_dir
}
cd $deploy_dir 
git checkout -B master origin/master -f
git reset origin/master
git pull
git tag -d $(git tag -l)
git fetch --tags
foreach($package in $packages){
  $global_tag_version="rel/${package}-${version}"
  if(!$(git ls-remote --tags origin | Select-String $global_tag_version)){
    git tag $global_tag_version
    git push origin $global_tag_version
  }
}
foreach($package in $packages){
  git checkout master
  git clean -f -d
  git rm -rf .
  git branch -D deploy
  git checkout --orphan deploy  
  git checkout master Packages/$package ./Packages/$package
  mv Packages/${package}/* .
  rm -r Packages
  git add .
  git commit -a -m"Updated packages to version $version"
  git tag -a ${package}-${version} -m"Version $version"
  git push origin ${package}-${version}
  # always create the 'latest' tag pointing to latest version
  $latest_tag=$(git ls-remote --tags origin | Select-String ${package}-latest)
  if ($latest_tag){
    git push --delete origin ${package}-latest
    git tag -d ${package}-latest
  }
  git tag -a ${package}-latest -m"Version $version"
  git push origin ${package}-latest
}

git checkout master
git branch -D deploy
cd ..
