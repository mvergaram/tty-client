# Todo: Generar .exe entregable, icono y renombrar a "TTY Dynamic Client"

Plan completo: `C:\Users\miguel.vergara\.claude\plans\considerando-este-proyecto-necesito-shimmering-blossom.md`

## Pasos

- [x] Crear `Assets\icon.ico` (placeholder generado con System.Drawing, reemplazable)
- [x] Editar `TtyClient.csproj`: AssemblyName, Product, AssemblyTitle, Version, ApplicationIcon, ItemGroup Resource
- [x] Editar `MainWindow.xaml`: `Icon=` y `Title=` → "TTY Dynamic Client — Skype for Business"
- [x] Editar `CLAUDE.md`: encabezado + comando de publish con single-file/self-contained
- [x] `dotnet build` para validar compilación (0 errores, 0 advertencias)
- [x] `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
- [x] Verificar que `publish\` no tiene DLLs nativas sueltas (solo `.exe` + `.pdb`)
- [x] Verificar metadata del .exe (ProductName/FileDescription/Version) y título de ventana en ejecución real

## Revisión

Todo lo planificado se implementó y se verificó funcionando:

- `Assets\icon.ico` — icono placeholder (círculo + "TTY" sobre fondo oscuro Catppuccin), reemplazable sobrescribiendo el archivo sin tocar código.
- `TtyClient.csproj` — agregadas `AssemblyName`, `Product`, `AssemblyTitle`, `Version`, `ApplicationIcon` y el `ItemGroup` con `Resource` del icono.
- `MainWindow.xaml` — `Icon="Assets/icon.ico"` agregado; `Title` actualizado a "TTY Dynamic Client — Skype for Business".
- `CLAUDE.md` — encabezado renombrado y comando de publish documentado con `PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`.
- `dotnet build` → compilación correcta sin errores.
- `dotnet publish ...` → generó `bin\Release\net10.0-windows\win-x64\publish\TtyClient.exe` (173 MB, self-contained) junto con `TtyClient.pdb` (símbolos de depuración — no es una DLL nativa, es opcional de excluir con `-p:DebugType=none` si se quiere literalmente un solo archivo).
- Metadata verificada vía `(Get-Item .\TtyClient.exe).VersionInfo`: `ProductName = "TTY Dynamic Client - Skype for Business"`, `FileDescription = "TTY Dynamic Client"`, `Version = 1.0.0`.
- Ejecutable lanzado realmente (`Start-Process`) y confirmado en ejecución con `MainWindowTitle = "TTY Dynamic Client — Skype for Business"`, luego cerrado limpiamente.

**Límite de verificación**: no se pudo confirmar visualmente (captura de pantalla) que el icono se vea correctamente en la barra de título/taskbar, ya que no hay herramienta de captura de ventanas nativas de Windows disponible en esta sesión (solo herramientas de navegador). Se recomienda al usuario confirmarlo visualmente una vez abra la app.
