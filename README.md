# Flight Simulator Enhanced Traffic

This **standalone** Flight Simulator addon, injects more accurate live traffic into your game!

# Installation

- Download the latest release from https://flightsim.to or https://github.com/GewoonJaap/Flight-Simulator-Better-Traffic
- Unzip the file
- Configure `Config/ModelMatching.json` to your needs (You don't need to insert your liveries here! Only planes.)
- Open `Enhanced Live Traffic.exe`
- Press `Connect`, once your simulator is loaded and your plane is spawned in.


# Model Matching
This application supports custom model matching
- Go to: `Config/ModelMatching.json`
- Edit the file accordingly.

## How to edit?

- How does model matching work? Lets say the `Boeing 787-9 Dreamliner` is detected, the modelmatcher will now search for an aircraft with the key: `Boeing 787-9 Dreamliner`, this will return `Boeing 787-10` (`"Boeing 787-9 Dreamliner": "Boeing 787-10"` as seen in the json file).
- The modelmatcher will now append the livery name, lets say "KLM", this results in: `Boeing 787-10 KLM`, if this livery is found, it will use this aircraft, if it isn't, it will fallback to: `Boeing 787-10 Default` (It appends default to the value gotten from the key),
- When getting `Boeing 787-10 Default` it will return `Boeing 787-10 Asobo` and use this aircraft.