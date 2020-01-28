#!/usr/bin/env bash

# This script will create a new version of all stackray packages, providing  the version as parameter
# NOTE: the script will go to `master` if not already there.

if [ -z "$1" ]
  then
    echo "You must supply a version."
    return
fi

declare -a packages=(
                "com.stackray.renderer"
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
git checkout -B master origin/master
git reset origin/master
git pull
git branch deploy
git checkout --orphan deploy    
for package in "${packages[@]}"
do
  git reset --hard HEAD~1
  git clean -f -d
  git rm -rf .
  git checkout master Packages/$package ./Packages/$package
  mv Packages/$package/*.* .
  rm -r Packages
  git add .
  git commit -a -m"Updated packages to version $1"
  git tag -a $package-$1 -m"Version $1"
  git push origin $package-$1
  # always create the 'latest' tag pointing to latest version
  latest_exist=$(git show-ref refs/heads/$package-latest)
  if [ -z "$latest_exist" ]
  then
    git push --delete origin $package-latest
    git tag -d $package-latest
  fi
  git tag -a $package-latest  -m"Version $1"
  git push origin $package-latest
done

git checkout master
git branch -D deploy
cd ..