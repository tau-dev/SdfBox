@echo off

echo ======== Vertex
fxc /E VS /T vs_5_0 Vertex.hlsl /Fo Vertex.hlsl.bytes /nologo
echo ======== Fragment
fxc /Zi /E FS /T ps_5_0 DisplayFrag.hlsl /Fo DisplayFrag.hlsl.bytes /nologo
echo ======== Compute
fxc /Zi /Od /E CS /T cs_5_0 Compute.hlsl /Fo Compute.hlsl.bytes /nologo
echo .
PAUSE