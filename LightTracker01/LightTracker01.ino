/*
    LightTracker1
      Djordje Herceg, 9.7.2024 - Concept, Hardware, 3D design and printing
      Aleksandar Horvat - Software, Documentation, Interactive capabilities

    ==Libraries==========
      ESP32Servo
      BH1750

    ==Hardware components==========
      SG-90 servo x 2
      BH1750 Ambient Light Sensor x 1
      LED x 1
  
    ==Hardware connection scheme==========
      ## BH1750
      VCC -- 3.3V
      GND -- GND
      SCL -- ESP pin 22
      SDA -- ESP pin 21
      ADDR -- GND

      ## SG-90 SERVO
      Brown (top) - GND
      Red (middle) - 5V
      Orange (bottom) - PWM

    == DESCRIPTION ==========
    This version uses a ONE_TIME_HIGHRES_MODE which is slow. It takes almost one second to obtain a reading.

    == PROJECT ASSIGNMENT ==========
    Implement the following commands:
    READ vangle, hangle - move the sensor to (vangle, hangle), read and return the lux value
    TABLE vstart, vend, hstart, hend - move the sensor from hstart to hend in 5 degree steps, then move from vstart to vend in 5 degree steps and take a light reading at each step. Return the table of read values

    In each case, the read value is {vangle, hangle, lux}
    THe table has the shape  
    {
      { {vangle11, hangle11, lux11}, {vangle12, hangle12, lux12}, {vangle1n, hangle1n, lux1n} },
      { {vangle21, hangle21, lux21}, {vangle22, hangle22, lux22}, {vangle2n, hangle2n, lux2n} },
      ...
      { {vanglem1, hanglem1, luxm1}, {vanglem2, hanglem2, luxm2}, {vanglemn, hanglemn, luxmn} }
    }

    Sweep strategy: for each vertical angle, sweeep horizontally from hmin to hmax, then increase vangle and then sweep from hmax to hmin.

*/


// These are all GPIO pins on the ESP32
// Recommended pins include 2,4,12-19,21-23,25-27,32-33

#define LED 33
#define S1 19  // vertikalni servo
#define S2 18  // horizontalni servo

#include <ESP32Servo.h>

Servo vertical;    // create servo object to control a servo
Servo horizontal;  // create servo object to control a servo
// 16 servo objects can be created on the ESP32

char serialbuf[80];

#include <BH1750.h>
#include <string>
#include <stdexcept>
#include <Wire.h>

BH1750 lightMeter;


void setup() {
  // put your setup code here, to run once:
  Serial.begin(115200);
  Serial.println("LightTracker1 - Djordje Herceg, Aleksandar Horvat 9.7.2024.");
  Serial.println("Enter the angles a,b where a is [0,90] and b is [0, 180] to position the sensor.");
  Serial.println("Enter any string to read the light intensity");

  pinMode(LED, OUTPUT);
  digitalWrite(LED, HIGH);
  delay(250);
  digitalWrite(LED, LOW);


  // Allow allocation of all timers
  ESP32PWM::allocateTimer(0);
  ESP32PWM::allocateTimer(1);
  ESP32PWM::allocateTimer(2);
  ESP32PWM::allocateTimer(3);
  vertical.setPeriodHertz(50);       // standard 50 hz servo
  vertical.attach(S1, 700, 2900);    // attaches the servo on pin 18 to the servo object
  horizontal.setPeriodHertz(50);     // standard 50 hz servo
  horizontal.attach(S2, 700, 2900);  // attaches the servo on pin 18 to the servo object
  // using default min/max of 1000us and 2000us
  // different servos may require different min/max settings
  // for an accurate 0 to 180 sweep


  // Initialize the I2C bus (BH1750 library doesn't do this automatically)
  Wire.begin();
  // On esp8266 you can select SCL and SDA pins using Wire.begin(D4, D3);
  //lightMeter.begin(BH1750::ONE_TIME_HIGH_RES_MODE);
  lightMeter.configure(BH1750::CONTINUOUS_LOW_RES_MODE);
  lightMeter.begin();
}

int h0, v0;  // uglovi
// forward deklaracije zato što je C++ kompajler glup
void move(int v, int h);
int readlight();

void loop() {
  // put your main code here, to run repeatedly:
  if (readline(Serial.read(), serialbuf, 80) > 0) {
    //Serial.print("You entered: >");
    //Serial.print(serialbuf);
    //Serial.println("<");

    std::string s = serialbuf;
    int f = s.find(",");
    //Serial.printf("Delimiter at position: %d\n", f);
    if (f > -1) {
      // sada imamo dva podstringa razdvojena zarezom
      int len2 = s.size() - f + 1;
      std::string prvi = s.substr(0, f);
      std::string drugi = s.substr(f + 1, len2);
      //Serial.printf("prvi: %s\n", prvi.c_str());
      //Serial.printf("drugi: %s\n", drugi.c_str());

      // ako ima išta pre i posle zareza, pretvori to u brojeve
      if ((f > 0) && (len2 > 0)) {
        // primer parsiranja stringa u integer sa https://en.cppreference.com/w/cpp/string/basic_string/stol
        try {
          if (prvi == "READ") {
            int vangle, hangle;
            if (parseAngles(drugi, vangle, hangle)) {
              move(vangle, hangle);
              delay(100);
              int lux = readlight();
              Serial.printf("{%d, %d, %d}\n", vangle, hangle, lux);
            }
          } else if (prvi == "TABLE") {
            int vstart, vend, hstart, hend;
            if (parseTableArgs(drugi, vstart, vend, hstart, hend)) {
              scanTable(vstart, vend, hstart, hend);
              move(0, 0);
            }
          } else if (prvi == "TABLE2") {
            int vstart, vend, hstart, hend;
            if (parseTableArgs(drugi, vstart, vend, hstart, hend)) {
              scanTable2(vstart, vend, hstart, hend);
            }
          }

        } catch (std::invalid_argument const &ex) {
          Serial.println("!Invalid argument");
        } catch (std::out_of_range const &ex) {
          Serial.println("!Out of range");
        }
      }
    } else {
      int lux = readlight();
      Serial.printf("{%d}\n", lux);
    }
  }
  ledBlink();
}


void move(int v, int h) {
  if (v < 0) v = 0;
  if (v > 90) v = 90;
  if (h < 0) h = 0;
  if (h > 180) h = 180;

  // set the global variables
  h0 = h;
  v0 = v;

  vertical.write(90 - v);  // zato sto je servo na obrnutu stranu.
  yield();
  horizontal.write(h);
  yield();
  //Serial.printf("P:%i,%i\n", v, h);
}

int readlight() {
  // https://github.com/claws/BH1750
  // Low Resolution Mode - (4 lx precision, 16ms measurement time)
  // High Resolution Mode - (1 lx precision, 120ms measurement time)
  // High Resolution Mode 2 - (0.5 lx precision, 120ms measurement time)
  //lightMeter.configure(BH1750::ONE_TIME_HIGH_RES_MODE);
  //lightMeter.configure(BH1750::CONTINUOUS_HIGH_RES_MODE);

  while (!lightMeter.measurementReady(true)) {
    yield();
  }
  float lux = lightMeter.readLightLevel();
  // adjust sensitivity and read again
  // if (lux < 0) {
  //   //Serial.println(F("!Error condition detected"));
  // } else {
  //   if (lux > 40000.0) {
  //     // reduce measurement time - needed in direct sun light
  //     if (lightMeter.setMTreg(32)) {
  //       //Serial.println(F("!Setting MTReg to low value for high light environment"));
  //     } else {
  //       //Serial.println(          F("!Error setting MTReg to low value for high light environment"));
  //     }
  //   } else {
  //     if (lux > 10.0) {
  //       // typical light environment
  //       if (lightMeter.setMTreg(69)) {
  //         //Serial.println(F("!Setting MTReg to default value for normal light environment"));
  //       } else {
  //         //Serial.println(F("!Error setting MTReg to default value for normal light environment"));
  //       }
  //     } else {
  //       if (lux <= 10.0) {
  //         // very low light environment
  //         if (lightMeter.setMTreg(138)) {
  //           //Serial.println(F("!Setting MTReg to high value for low light environment"));
  //         } else {
  //           //Serial.println(F("!Error setting MTReg to high value for low light environment"));
  //         }
  //       }
  //     }
  //   }
  // }

  // while (!lightMeter.measurementReady(true)) {
  //   yield();
  // }
  // lux = lightMeter.readLightLevel();

  //Serial.printf("{%d, %d, %f}\n", h0, v0, lux);

  //lightMeter.configure(BH1750::ONE_TIME_HIGH_RES_MODE);
  //lightMeter.configure(BH1750::CONTINUOUS_HIGH_RES_MODE);

  return (int)lux;  // if return is omitted, the program freezes?
}


void ledBlink() {
  static unsigned long mils = millis();
  static bool led = 0;
  if (millis() - mils > 1000) {
    mils = millis();
    digitalWrite(LED, led);
    led = !led;
  }
}

int readline(int readch, char *buffer, int len) {
  static int pos = 0;
  int rpos;

  if (readch > 0) {
    switch (readch) {
      case '\r':  // Ignore CR
        break;
      case '\n':  // Return on new-line
        rpos = pos;
        pos = 0;  // Reset position index ready for next time
        return rpos;
      default:
        if (pos < len - 1) {
          buffer[pos++] = readch;
          buffer[pos] = 0;
        }
    }
  }
  return 0;
}

bool parseAngles(const std::string &drugi, int &vangle, int &hangle) {
  int f = drugi.find(",");
  if (f == -1) return false;
  std::string vstr = drugi.substr(0, f);
  std::string hstr = drugi.substr(f + 1);
  try {
    vangle = std::stoi(vstr);
    hangle = std::stoi(hstr);
    return true;
  } catch (...) {
    Serial.println("Invalid angles");
    return false;
  }
}

bool parseTableArgs(const std::string &drugi, int &vstart, int &vend, int &hstart, int &hend) {
  int f1 = drugi.find(",");
  if (f1 == -1) return false;
  std::string vstartstr = drugi.substr(0, f1);

  int f2 = drugi.find(",", f1 + 1);
  if (f2 == -1) return false;
  std::string vendstr = drugi.substr(f1 + 1, f2 - f1 - 1);

  int f3 = drugi.find(",", f2 + 1);
  if (f3 == -1) return false;
  std::string hstartstr = drugi.substr(f2 + 1, f3 - f2 - 1);
  std::string hendstr = drugi.substr(f3 + 1);

  try {
    vstart = std::stoi(vstartstr);
    vend = std::stoi(vendstr);
    hstart = std::stoi(hstartstr);
    hend = std::stoi(hendstr);
    return true;
  } catch (...) {
    Serial.println("Invalid table args");
    return false;
  }
}
/*
    {
      { {vangle11, hangle11, lux11}, {vangle12, hangle12, lux12}, {vangle1n, hangle1n, lux1n} },
      { {vangle21, hangle21, lux21}, {vangle22, hangle22, lux22}, {vangle2n, hangle2n, lux2n} },
      ...
      { {vanglem1, hanglem1, luxm1}, {vanglem2, hanglem2, luxm2}, {vanglemn, hanglemn, luxmn} }
    }
*/
void scanTable(int vstart, int vend, int hstart, int hend) {
  Serial.println("{");
  bool reverse = false;
  for (int v = vstart; v <= vend; v += 5) {
    Serial.print("  { ");
    if (!reverse) {
      for (int h = hstart; h <= hend; h += 5) {
        move(v, h);
        delay(100);
        int lux = readlight();
        Serial.printf("{%d, %d, %d}", v, h, lux);
        if (h < hend) Serial.print(", ");
      }
    } else {
      for (int h = hend; h >= hstart; h -= 5) {
        move(v, h);
        delay(100);
        int lux = readlight();
        Serial.printf("{%d, %d, %d}", v, h, lux);
        if (h > hstart) Serial.print(", ");
      }
    }
    reverse = !reverse;
    Serial.print(" }");
    if (v < vend) Serial.print(",\n");
  }
  Serial.println("\n}");
}

void scanTable2(int vstart, int vend, int hstart, int hend) {
  Serial.print("{");  // Start of the list
  bool reverse = false;
  for (int v = vstart; v <= vend; v += 5) {
    Serial.print("{");  // Start of a nested list
    if (!reverse) {
      for (int h = hstart; h <= hend; h += 5) {
        move(v, h);
        delay(100);
        int lux = readlight();
        Serial.print("{");
        Serial.print(v);
        Serial.print(", ");
        Serial.print(h);
        Serial.print(", ");
        Serial.printf("%d", lux);
        Serial.print("}");
        if (h < hend) Serial.print(",");
      }
    } else {
      for (int h = hend; h >= hstart; h -= 5) {
        move(v, h);
        delay(100);
        int lux = readlight();
        Serial.print("{");
        Serial.print(v);
        Serial.print(", ");
        Serial.print(h);
        Serial.print(", ");
        Serial.printf("%d", lux);
        Serial.print("}");
        if (h > hstart) Serial.print(",");
      }
    }
    reverse = !reverse;
    Serial.print("}");
    if (v < vend) Serial.print(",");  // Add comma between nested lists
  }
  Serial.print("}");  // End of the list
}