#!/usr/bin/env bash

# This script will create a branch for each package

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
  git checkout --orphan $package
  git rm -rf .
  git checkout master Packages/$package ./Packages/$package
  mv Packages/$package/*.* .
  rm -r Packages
  git add .
  git commit -a -m"$package Package"
  git push --set-upstream origin $package
done

git checkout master
