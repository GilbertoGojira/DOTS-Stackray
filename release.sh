#!/usr/bin/env bash

# This script will create a new version of all stackray packages, providing  the version as parameter
# NOTE: the script will go to `master` if not already there.

if [ -z "$1" ]
  then
    echo "You must supply a version."
    return
fi

global_tag_version="rel/v$1"
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
git tag $global_tag_version
git push origin $global_tag_version
for package in "${packages[@]}"
do
  git checkout master
  git clean -f -d
  git rm -rf .
  git branch -D deploy
  git checkout --orphan deploy  
  git checkout master Packages/$package ./Packages/$package
  mv Packages/$package/*.* .
  rm -r Packages
  git add .
  git commit -a -m"Updated packages to version $1"
  git tag -a $package-$1 -m"Version $1"
  git push origin $package-$1
  # always create the 'latest' tag pointing to latest version
  latest_tag=$(git ls-remote --tags origin | grep $package-latest)
  if [ ! -z "$latest_tag" ]
  then
    git push --delete origin $package-latest
    git tag -d $package-latest
  fi
  git tag -a $package-latest -m"Version $1"
  git push origin $package-latest
done

git checkout master
git branch -D deploy
cd ..