#!/bin/sh

case "$1" in

 'clean')
    dotnet bin/prebuild.dll /file prebuild.xml /clean

  ;;


  'autoclean')

    echo y|dotnet bin/prebuild.dll /file prebuild.xml /clean

  ;;



  *)

    cp bin/System.Drawing.Common.dll.linux bin/System.Drawing.Common.dll
    # Bundled Prebuild supports net8_0 project generation, then the SDK
    # projects are retargeted to .NET 10 until its bootstrap is modernized.
    DOTNET_ROLL_FORWARD=Major dotnet bin/prebuild.dll /target vs2022 /targetframework net8_0 /excludedir = "obj | bin" /file prebuild.xml || exit $?
    python3 tools/build/retarget_net10.py || exit $?
    echo "dotnet build -c Release OpenSim.sln" > compile.sh
    chmod +x compile.sh

  ;;

esac
