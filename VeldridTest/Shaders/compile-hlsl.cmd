@echo off

echo ======== Vertex
dxc /E VS /T vs_5_0 Vertex.hlsl /Fo Vertex.hlsl.bytes /nologo
echo ======== Fragment
dxc /Zi /E FS /T ps_5_0 DisplayFrag.hlsl /Fo DisplayFrag.hlsl.bytes /nologo
dxc /Zi /E FS /T ps_5_0 Plain.hlsl /Fo Plain.hlsl.bytes /nologo
echo ======== Compute
dxc /Zi /Od /E main /T cs_5_0 Compute.hlsl /Fo Compute.hlsl.bytes /nologo
echo .
PAUSE