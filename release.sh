#!/usr/bin/env bash

# This script will create a new version of all stackray packages or for just one package, providing the version as parameter or the package name and the version
# NOTE: the script will go to `master` if not already there.

all_packages=true
version=$1
if [ -z "$version" ]
  then
    echo "You must supply a version for all packages or a package name and a version"
    return
fi
declare -a packages=(
                "com.stackray.renderer"
                "com.stackray.sprite"
                "com.stackray.entities"
                "com.stackray.mathematics"
                "com.stackray.collections"
                "com.stackray.jobs"
                "com.stackray.burst"
                "com.stackray.transforms"
                "com.stackray.text"
                )
if [ "$#" -eq 2 ]
then
    packages=($1)
    version=$2
    all_packages=false
fi
deploy_dir="Deploy"     
if [[ ! -e $deploy_dir ]]; then
  mkdir $deploy_dir
  git clone git@github.com:GilbertoGojira/DOTS-Stackray.git $deploy_dir
fi 
cd $deploy_dir 
git checkout -B master origin/master -f
git reset origin/master
git pull
git tag -d $(git tag -l)
git fetch --tags
for package in "${packages[@]}"
do
  $global_tag_version="rel/${package}-${version}"
  $global_tag_exists=$(git ls-remote --tags origin | Select-String $global_tag_version)
  if [ -z "$global_tag_exists" ]
  then
    git tag $global_tag_version
    git push origin $global_tag_version
  fi
done

for package in "${packages[@]}"
do
  git checkout master
  git clean -f -d
  git rm -rf .
  git branch -D deploy
  git checkout --orphan deploy  
  git checkout master Packages/$package ./Packages/$package
  mv Packages/$package/* .
  rm -r Packages
  git add .
  git commit -a -m"Updated packages to version $version"
  git tag -a $package-$version -m"Version $version"
  git push origin $package-$version
  # always create the 'latest' tag pointing to latest version
  latest_tag=$(git ls-remote --tags origin | grep $package-latest)
  if [ ! -z "$latest_tag" ]
  then
    git push --delete origin $package-latest
    git tag -d $package-latest
  fi
  git tag -a $package-latest -m"Version $version"
  git push origin $package-latest
done

git checkout master
git branch -D deploy
cd ..
