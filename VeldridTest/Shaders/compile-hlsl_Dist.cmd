@echo off

echo ======== Compute
fxc /Zi /Od /E CS /T cs_5_0 DistFind.hlsl /Fo DistFind.hlsl.bytes /nologo
echo .
PAUSE