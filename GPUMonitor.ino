#include <FastLED.h>
#include <EEPROM.h> // Added for saving settings

// --- PIN DEFINITIONS ---
#define DATA_PIN 6      // Pin connected to the ARGB LED strip data line
#define BTN1_PIN 3      // Pin connected to Button 1 (Color Profile)
#define BTN2_PIN 4      // Pin connected to Button 2 (Brightness)

// --- HARDWARE CONFIGURATION ---
#define NUM_LEDS 60     // Total number of LEDs on the strip
#define BRIGHTNESS_STEPS 6
#define DEBOUNCE_DELAY 80 // Milliseconds for button debounce (increased for breadboard safety)

// --- EEPROM ADDRESSES ---
#define EEPROM_ADDR_PROFILE 0
#define EEPROM_ADDR_BRIGHTNESS 1

// --- INITIALIZATION ---
CRGB leds[NUM_LEDS];

// Gamma-corrected brightness levels: mapped so 0%, 20%, 40%, 60%, 80%, 100% LOOK equal to the human eye
uint8_t brightnessLevels[BRIGHTNESS_STEPS] = {0, 8, 33, 81, 155, 255};

// State variables
int gpu = 0;
int activeLeds = 0;
int currentProfile = 1;   // Default fallback
int brightnessIndex = 5;  // Default fallback

// Button debouncing variables
int btn1State = HIGH;
int lastBtn1State = HIGH;
unsigned long lastBtn1DebounceTime = 0;

int btn2State = HIGH;
int lastBtn2State = HIGH;
unsigned long lastBtn2DebounceTime = 0;

void setup() {
  Serial.begin(19200);
  
  // Initialize buttons with internal pull-up resistors
  pinMode(BTN1_PIN, INPUT_PULLUP);
  pinMode(BTN2_PIN, INPUT_PULLUP);

  // --- LOAD SAVED SETTINGS FROM EEPROM ---
  currentProfile = EEPROM.read(EEPROM_ADDR_PROFILE);
  // Bounds check: If EEPROM is empty (reads 255) or out of bounds, reset to 1
  if (currentProfile < 1 || currentProfile > 6) currentProfile = 1;

  brightnessIndex = EEPROM.read(EEPROM_ADDR_BRIGHTNESS);
  // Bounds check: If EEPROM is empty or out of bounds, reset to 5 (100%)
  if (brightnessIndex >= BRIGHTNESS_STEPS) brightnessIndex = 5;

  // Initialize FastLED
  // Note: PC ARGB strips are typically GRB, not RGB
  FastLED.addLeds<WS2812B, DATA_PIN, GRB>(leds, NUM_LEDS);
  FastLED.setBrightness(brightnessLevels[brightnessIndex]); // Use loaded brightness for startup
  FastLED.clear();

  // --- STARTUP SEQUENCE ---
  for (int i = 0; i < NUM_LEDS; i++) {
    leds[i] = CRGB::White;
    FastLED.show();
    delay(50);
  }
  delay(500); // Pause to show all LEDs lit
  FastLED.clear();
  FastLED.show();
}

void loop() {
  // 1. Read GPU data from PC tool
  if (Serial.available()) {
    gpu = Serial.parseInt();
    // Constrain the value just in case the tool sends out-of-bounds data
    gpu = constrain(gpu, 0, 100); 
  }

  // 2. Calculate linear number of active LEDs
  activeLeds = map(gpu, 0, 100, 0, NUM_LEDS);

  // 3. Handle Button 1 Presses (Profile Cycling)
  int btn1Reading = digitalRead(BTN1_PIN);
  if (btn1Reading != lastBtn1State) {
    lastBtn1DebounceTime = millis();
  }
  if ((millis() - lastBtn1DebounceTime) > DEBOUNCE_DELAY) {
    if (btn1Reading != btn1State) {
      btn1State = btn1Reading;
      // Button is pressed when reading is LOW (due to pull-up)
      if (btn1State == LOW) {
        currentProfile++;
        if (currentProfile > 6) currentProfile = 1;
        
        // --- SAVE TO EEPROM ---
        EEPROM.write(EEPROM_ADDR_PROFILE, currentProfile);
      }
    }
  }
  lastBtn1State = btn1Reading;

  // 4. Handle Button 2 Presses (Brightness Cycling)
  int btn2Reading = digitalRead(BTN2_PIN);
  if (btn2Reading != lastBtn2State) {
    lastBtn2DebounceTime = millis();
  }
  if ((millis() - lastBtn2DebounceTime) > DEBOUNCE_DELAY) {
    if (btn2Reading != btn2State) {
      btn2State = btn2Reading;
      if (btn2State == LOW) {
        brightnessIndex++;
        if (brightnessIndex >= BRIGHTNESS_STEPS) brightnessIndex = 0;
        FastLED.setBrightness(brightnessLevels[brightnessIndex]);
        
        // --- SAVE TO EEPROM ---
        EEPROM.write(EEPROM_ADDR_BRIGHTNESS, brightnessIndex);
      }
    }
  }
  lastBtn2State = btn2Reading;

  // 5. Update LED colors based on Profile
  FastLED.clear(); // Turn off all LEDs before applying new state

  if (currentProfile == 6) {
    // Profile 6: All off (FastLED.clear() already did this)
  } 
  else {
    for (int i = 0; i < activeLeds; i++) {
      switch (currentProfile) {
        case 1: leds[i] = CRGB::White; break;
        case 2: leds[i] = CRGB::Blue; break;
        case 3: leds[i] = CRGB::Cyan; break;
        
        case 4: 
          // Thermometer: Color based on LED physical position (0 to 25)
          leds[i] = getLoadColor((float)i / (NUM_LEDS - 1)); 
          break;
          
        case 5: 
          // Solid Color: Color based on actual GPU percentage
          leds[i] = getLoadColor((float)gpu / 100.0); 
          break;
      }
    }
  }

  // 6. Send data to LEDs
  FastLED.show();
}

// --- HELPER FUNCTION ---
// Calculates the gradient color based on a 0.0 to 1.0 percentage input
CRGB getLoadColor(float percent) {
  percent = constrain(percent, 0.0, 1.0);
  
  if (percent <= 0.60) {
    return CRGB(0, 255, 0); // Green
  } 
  else if (percent <= 0.70) {
    // Green to Yellow gradient
    float t = (percent - 0.60) / 0.10; 
    return CRGB((uint8_t)(255 * t), 255, 0);
  } 
  else if (percent <= 0.80) {
    return CRGB(255, 255, 0); // Yellow
  } 
  else if (percent <= 0.90) {
    // Yellow to Red gradient
    float t = (percent - 0.80) / 0.10;
    return CRGB(255, (uint8_t)(255 * (1.0 - t)), 0);
  } 
  else {
    return CRGB(255, 0, 0); // Red
  }
}
