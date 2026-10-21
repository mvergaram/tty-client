# TTY Dynamic Client — Skype for Business

Cliente de software TTY/TDD para Windows 11 que captura el audio de llamadas de Skype for Business y decodifica/codifica tonos FSK Baudot en tiempo real.

## Requisitos

- Windows 11
- .NET 10 Desktop Runtime (o compilar/publicar self-contained, ver más abajo)
- **[VB-CABLE Virtual Audio Device](https://vb-audio.com/Cable/)** — driver de audio virtual gratuito, **obligatorio** para enviar los tonos TTY sintetizados a Skype for Business

### ¿Por qué se necesita VB-CABLE?

Skype for Business no permite inyectar audio arbitrario directamente: solo transmite lo que recibe desde el dispositivo configurado como **micrófono**. VB-CABLE resuelve esto creando un par de dispositivos de audio virtuales en Windows:

> Simple & Easy! All audio streams sent to VB-CABLE inputs (Playback Device) will be forwarded to VB-CABLE outputs (Recording Device).

En otras palabras: todo lo que esta app reproduce hacia **"CABLE Input"** (que Windows ve como un dispositivo de reproducción) aparece automáticamente en **"CABLE Output"** (que Windows ve como un dispositivo de grabación). Esto permite usar "CABLE Output" como si fuera un micrófono real.

Flujo concreto en esta app:

1. El usuario escribe un mensaje y la app lo codifica como tonos FSK (Baudot ITA-2).
2. La app reproduce esos tonos hacia **CABLE Input** (dispositivo "Salida de audio" en la UI).
3. VB-CABLE reenvía esa señal a **CABLE Output**.
4. En Skype for Business, configurado con **CABLE Output** como micrófono, los tonos se transmiten como si fueran voz del usuario.

### Instalación y configuración

1. Descargar e instalar VB-CABLE desde <https://vb-audio.com/Cable/>.
2. En Skype for Business → Configuración de audio → **Micrófono**: seleccionar `CABLE Output (VB-Audio Virtual Cable)`.
3. En la app, en el selector **"Salida de audio"**, elegir `CABLE Input (VB-Audio Virtual Cable)`.
4. La llamada debe conectarse a un **número PSTN real** con el gateway/SIP trunk en **G.711 sin transcodificación** — las llamadas SfB-a-SfB usan Silk/RTAudio y no preservan los tonos FSK.

> VB-CABLE es un driver de kernel; puede requerir aprobación de IT en entornos corporativos.

## Compilar y ejecutar

```bash
dotnet build
dotnet run
```

## Publicar ejecutable autónomo

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

El `.exe` resultante queda en `bin\Release\net10.0-windows\win-x64\publish\TtyClient.exe`.

Más detalles de arquitectura, pipeline de audio y parámetros TTY en [`CLAUDE.md`](CLAUDE.md).
