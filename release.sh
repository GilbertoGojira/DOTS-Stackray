#!/usr/bin/env bash

# This script will create a new version of all stackray packages, providing  the version as parameter
# NOTE: the script will go to `master` if not already there.

if [ -z "$1" ]
  then
    echo "You must supply a version."
    return
fi

declare -a packages=(
                "com.stackray.spriterenderer"
                "com.stackray.entities" 
                "com.stackray.mathematics"
                "com.stackray.collections"
                "com.stackray.jobs"
                "com.stackray.transforms"
                "com.stackray.text"
                )
                
for package in "${packages[@]}"
do
  branch_exist=$(git show-ref refs/heads/$package) 
  if [ -z "$branch_exist" ]
  then
    git checkout --orphan $package
    git rm -rf .
    git checkout master Packages/$package ./Packages/$package
    mv Packages/$package/*.* .
    rm -r Packages
    git add .
    git commit -a -m"$package Package"
    git push --set-upstream origin $package
  fi 
  git checkout $package
  git checkout master Packages/$package ./Packages/$package
  cp -rlf Packages/$package/*.* .
  rm -r Packages
  git commit -a -m"$Updated packages to version $1"
  git push
  git tag $package-$1
  git push origin $package-$1
  # always create the 'latest' tag pointing to latest version
  latest_exist=$(git show-ref refs/heads/$package-latest)
  if [ -z "$latest_exist" ]
  then
    git push --delete origin $package-latest
    git tag -d $package-latest
  fi
  git tag $package-latest
  git push origin $package-latest
done

git checkout master
