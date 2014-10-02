NDepCheck
=========

__NDepCheck__ is a tool that helps you to keep the (static) architecture of a .NET software clean. You specify allowed dependencies between namespaces, classes, or even methods, and __NDepCheck__ will check whether the dependencies are violated somewhere. You will use this in Nightly Builds or Continuous Integration Builds to prevent the introduction of unwanted dependencies.

Ideally, the software architect will specify the intended dependencies (or rather, dependency rules) for some module before code is written. However, by creating a diagram of the dependencies, it is also possible to explore and document the dependencies of an existing piece of software. Both ways of proceeding are especially useful to prevent the dreaded cyclic dependencies, which usually result in a tangled monolith of interdependencies, making a software unmaintainable in a quite short period of time.

__NDepCheck__ has proven its usefulness (and stability) in a project of 25 developers with now more than 2 million LOC.

[![Build status](https://ci.appveyor.com/api/projects/status/addnxwsk0aba24a6/branch/master)](https://ci.appveyor.com/project/thoemmi/ndepcheck/branch/master)
