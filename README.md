# Arduino-ARGB-GPU-Monitor
This GPU meter displays GPU usage using an ARGB LED strip. It has several color profiles stored, and you can adjust the brightness. The most recently used settings are saved in the Arduino and loaded the next time it is restarted or the USB cable is reconnected.

![](https://github.com/JohnConner0815/ArduinoGpuMonitor/blob/main/Chematic.png)
1. Open GPUMonitor.ino and adjust the number of LEDs in line 10 below “// --- HARDWARE CONFIGURATION ---” depending on how many you want to control. The scaling is done automatically. Save this to your file.
2. Upload GPUMonitor.ino to your board
4. You need the following files in the same folder: **ArduinoGpuMonitor.exe, System.Memory.dll, System.Numerics.Vectors.dll, System.Runtime.CompilerServices.Unsafe.dll, LibreHardwareMonitorLib.dll**
5. Run ArduinoGpuMonitor.exe
6. Select your Com-port, your GPU (if have more than one), refresh time (this affects the LED-strip and the response time of your buttons) and press connect
7. Select the colour profile by pressing the Button which is connected to Pin 3
8. The following colour profiles are available: 
 > - 1 White
 > - 2 Blue
 > - 3 Cyan
 > - 4 thermometer style with green up to 50%, then yellow at 75% and red at 90% and higher
 > - 5 similar to 4 but all used LEDs lit up in the colour depending on the GPU-usage
 > - 6 all LEDs are off
9. Select your brightness by pressing the Button connected to Pin 4
10. Brightness adjustment is logarithmic; it is therefore more of a gamma correction, which is more easily perceived by the human eye.
