#!/usr/bin/env bash

# This script will create a new version of all stackray packages, providing  the version as parameter
# NOTE: the script will go to `master` if not already there.

declare -a packages=(
                "com.stackray.spriterenderer"
                "com.stackray.entities" 
                "com.stackray.mathematics"
                "com.stackray.collections"
                "com.stackray.jobs"
                "com.stackray.transforms"
                )
                
for package in "${packages[@]}"
do
  git checkout $package
  git checkout master Packages/$package ./Packages/$package
  cp -rlf Packages/$package/*.* .
  rm -r Packages
  git commit -a -m"$Updated packages to version $1"
  git push
  git tag $package-$1
  git push origin $package-$1
done

git checkout master
