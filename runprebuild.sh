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
    dotnet bin/prebuild.dll /target vs2022 /targetframework net8_0 /excludedir = "obj | bin" /file prebuild.xml
    printf '%s\n' '#!/bin/sh' 'set -e' 'dotnet build -c Release OpenSim.sln' 'bash addon-modules/OGLAssetService/build.sh Release' 'bash addon-modules/OGLSeasons/build.sh Release' > compile.sh
    chmod +x compile.sh

  ;;

esac
