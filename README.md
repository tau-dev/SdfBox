# SdfBox

## Overview

This is an implementation of a Raytracer on adaptively sampled distance fields.
It relies heavily on the very nice abstraction layer of [Veldrid](https://veldrid.dev/).

## Use

Although adding additional compatibility should be pretty easy, I can currently only support Windows w/ DirectX11.
Besides Veldrid, the build relies on the Microsoft Guidelines Support Library.

The executable takes a path to a custom .asdf or .ply file<sup>1</sup>, generating an .asdf file from the latter.

Once loaded, the model should be displayed, and you can move the camera using Click+Drag / W,A,S,D,Shift,Ctrl.

<sup>1</sup>the ply parser is highly crappy and assumes the vertices are the first block in the file;
this is true for the files outputted by MeshLab, but not necessarily for yours.

## Plans

- Add support for modeling, as efficient space carving is one of the main benefits of distance fields.

- Extend the capabilities to full path tracing

## Main References

Sarah F. Frisken et al.: Adaptively sampled distance fields: A general representation of shape for computer graphics

Jakob A. Bærentzen and Henrik Aanæs: Signed distance computation using the angle weighted pseudonormal

Thiago Bastos and Waldemar Celes: Gpu-Accelerated Adaptively Sampled Distance Fields

