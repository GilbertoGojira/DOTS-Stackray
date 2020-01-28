# DOTS-Stackray
A collection of packages that extend and add new functionality on top of DOTS.

## Getting Started

Clone the repository and open Example project folder with Unity.
Included packages are:
  * Stackray Entities
  * Stackray Collection
  * Stackray Transforms
  * Stackray Burst
  * Stackray Mathematics
  * Stackray Jobs
  * Stackray Renderer
  * Stackray Text

### Prerequisites

Currently this project requires Unity 2019.3.0f1

### Installing

Simply clone the project and open Examples folder project and all the required packages will be imported.
In alternative each package can be added separately to the package manager by adding the git address the the correct tagged version.
For example to add Stackray Entities 0.0.1-preview1 add the following git address to package manager:
````
https://github.com/GilbertoGojira/DOTS-Stackray.git#com.stackray.entities-0.0.1-preview.1
````
or
```
git@github.com:GilbertoGojira/DOTS-Stackray.git#com.stackray.entities-0.0.1-preview.1
````
and for the laster version
```
git@github.com:GilbertoGojira/DOTS-Stackray.git#com.stackray.entities-latest
````

For each new version a tag will be created for each package.

## Package Overview
  * Stackray Entities - Extend query based native container operation
  * Stackray Collection - Extra native containers and extensions to operate on all the native container family
  * Stackray Transforms - Collection systems that operate on top of Unity.Transforms and extend it
  * Stackray Burst - Enables burst compilation of generic jobs
  * Stackray Mathematics - Some extra mathematics operations
  * Stackray Jobs - Simple jobs helpers
  * Stackray Renderer - Generic renderer with per instance property rendering using compute buffers (includes sprite rendering and support URP and HDRP)
  * Stackray Text - Text renderer system
