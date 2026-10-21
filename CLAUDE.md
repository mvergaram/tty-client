# TTY Dynamic Client — Skype for Business

Cliente de software TTY/TDD para Windows 11 que captura el audio de llamadas de Skype for Business y decodifica/codifica tonos FSK Baudot en tiempo real.

## Stack

- **Lenguaje**: C# 13 / .NET 10 (net10.0-windows)
- **UI**: WPF
- **Audio**: NAudio 2.2.1 (WASAPI loopback + WasapiOut)
- **Protocolo TTY**: Weitbrecht estándar EE.UU. — 45.45 baud, 1400/1800 Hz FSK, Baudot ITA-2

## Comandos

```bash
# Compilar
dotnet build

# Ejecutar
dotnet run

# Publicar ejecutable autónomo (un único .exe, sin DLLs nativas sueltas)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

El `.exe` resultante queda en `bin\Release\net10.0-windows\win-x64\publish\TtyClient.exe`.

- **Self-contained** (incluye el runtime de .NET 10): el `.exe` es más pesado pero corre en cualquier Windows 11 x64 sin instalar nada adicional. La alternativa framework-dependent da un `.exe` liviano pero exige tener el .NET 10 Desktop Runtime instalado en la máquina destino.
- **`IncludeNativeLibrariesForSelfExtract=true`**: las DLLs nativas de interop de WPF (`PresentationNative_cor3.dll`, `wpfgfx_cor3.dll`, `vcruntime140_cor3.dll`, etc.) quedan embebidas dentro del único `.exe` y se auto-extraen a una carpeta temporal en el primer arranque, en vez de quedar sueltas junto al `.exe`.
- **Icono y nombre**: configurados en `TtyClient.csproj` (`ApplicationIcon`, `Product`, `AssemblyTitle`, `Version`) y en el `Icon=`/`Title=` de `MainWindow.xaml`. El icono vive en `Assets\icon.ico` — reemplazar ese archivo y volver a publicar para cambiarlo.

## Estructura

```
tty-client/
├── Audio/
│   ├── AudioCapture.cs        # WASAPI loopback (Render) o captura real (Capture) según el dispositivo elegido
│   ├── AudioEndpointInfo.cs   # Id/FriendlyName/DataFlow de un endpoint seleccionable como "Entrada de audio"
│   ├── AudioResampler.cs      # Float32@inputRate → Int16@8kHz, lineal
│   └── VirtualMicInjector.cs  # Reproduce FSK en VB-Cable u otro dispositivo virtual (siempre Render)
├── TTY/
│   ├── GoertzelDetector.cs    # Potencia @ 1400 Hz vs 1800 Hz, bloques 20ms (160 samples)
│   ├── BitSynchronizer.cs     # FSM: IDLE → START_BIT → DATA_BITS(5) → STOP_BIT
│   ├── BaudotDecoder.cs       # ITA-2 5-bit → char, gestiona LTRS/FIGS shift
│   ├── BaudotEncoder.cs       # texto → List<byte> de códigos ITA-2 con shifts
│   └── FSKSynthesizer.cs      # Síntesis de senos 1400/1800 Hz, 45.45 baud, PCM 16-bit
├── MainWindow.xaml             # UI WPF (tema oscuro Catppuccin)
├── MainWindow.xaml.cs          # Conecta la pipeline y maneja eventos de UI
└── TtyClient.csproj
```

## Pipeline de decodificación

La UI tiene un selector "Entrada de audio" que lista TODOS los endpoints activos: primero los de Render (para loopback de una llamada SfB que suena por ese dispositivo) y luego los de Capture (micrófonos/handsets físicos reales, ej. un TTY acoplado acústicamente). `AudioCapture.Start(AudioEndpointInfo?)` decide `WasapiLoopbackCapture` o `WasapiCapture` según el `DataFlow` del dispositivo elegido:

```
SfB call audio (render device, loopback)        Micrófono/handset físico (capture device)
  → WasapiLoopbackCapture                          → WasapiCapture
                    \                                /
                     → Float32 → Int16 mono @8kHz (AudioResampler)
                     → Goertzel 1400 Hz / 1800 Hz, bloques 160 samples (GoertzelDetector)
                     → Decisión MARK/SPACE por bloque → BitSynchronizer (FSM)
                     → Bytes ITA-2 5-bit → BaudotDecoder
                     → char → UI (TextBlock)
```

Ambos caminos exigen formato de entrada float32 (`AudioCapture.EnsureSupportedFormat`); si el dispositivo entrega otro formato, se informa con un error claro en vez de decodificar basura.

## Pipeline de codificación (envío)

```
texto del usuario
  → BaudotEncoder → List<byte> ITA-2 con shifts
  → FSKSynthesizer → PCM 16-bit 8kHz (senos 1400/1800 Hz a 45.45 baud)
  → VirtualMicInjector → WasapiOut en VB-Cable
  → SfB transmite los tonos como si fuera voz del micrófono
```

## Parámetros TTY fijos

| Constante | Valor | Dónde |
|---|---|---|
| Frecuencia MARK | 1400 Hz | GoertzelDetector, FSKSynthesizer |
| Frecuencia SPACE | 1800 Hz | GoertzelDetector, FSKSynthesizer |
| Baud rate | 45.45 baud | FSKSynthesizer (SamplesPerBit = 176 @ 8kHz) |
| Sample rate interno | 8000 Hz | GoertzelDetector, FSKSynthesizer |
| Tamaño de bloque | 160 samples | GoertzelDetector (20ms) |
| Estructura de trama | 1 START + 5 DATA + 1.5 STOP | BitSynchronizer, FSKSynthesizer |

## Prerequisitos de entorno

1. **VB-Audio Virtual Cable** instalado (driver gratuito): <https://vb-audio.com/Cable/>
2. En Skype for Business → Configuración de audio → **Micrófono**: seleccionar `CABLE Input (VB-Audio Virtual Cable)`
3. La llamada SfB debe conectarse a un **número PSTN real** — las llamadas SfB-a-SfB usan Silk/RTAudio, que no preserva los tonos FSK
4. El gateway/SIP trunk debe estar configurado con **G.711 sin transcodificación**

## Verificar la configuración de SfB Server (PowerShell)

```powershell
Get-CsVoicePolicy | Select-Object Identity, EnableTTYTDD
Set-CsVoicePolicy -Identity "Global" -EnableTTYTDD $true
Get-CsTrunkConfiguration | Select-Object Identity, EnableReferSupport
```

## Limitaciones conocidas

- TTY audio **solo funciona sobre PSTN** (G.711 obligatorio). Llamadas internas SfB-a-SfB no son compatibles.
- El decodificador usa ventanas de 20ms sin solapamiento; en señales débiles puede perder transiciones de bit. Si hay errores frecuentes, reducir `BlockSize` en `GoertzelDetector` a 80 samples (10ms) y ajustar el umbral de decisión.
- VB-Cable es un driver de kernel; puede requerir aprobación de IT en entornos corporativos.
- SfB 2019 tiene soporte extendido hasta octubre 2029.

## TODO

- [ ] Revisar por qué la app se queda pegada (no responde) si se está escuchando y se presiona cerrar la ventana (X). Probable causa: `Window_Closing` llama a `_capture.Dispose()`/`_injector.Dispose()` sin detener antes la captura en curso desde el hilo de UI, bloqueando en el cierre del stream WASAPI activo.
