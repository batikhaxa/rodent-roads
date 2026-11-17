# Rodent Roads
Project to bring "rodent adventures" from Sims 4 into the Sims 3. 
While it takes inspiration from the sims 4, it will be my own flavour.

## Sims 4 description
(from wiki)
Rodents in My First Pet Stuff can travel to far-away lands. They sometimes leave their cage, and bring back a postcard from where they traveled to. A note can be seen in the cage, notifying that they have left. If Sims do not want their rodent to go on adventures, they can purchase the IQ limiter vet treat (§350) which will set their IQ to a normal level.

Notifications will inform the player about their rodent's adventures along the way. For example (RODENT indicates name):
> Tiny Rodent; Big Adventure 
> RODENT was thinking about you during its travels, and brought a postcard back to share! OR Rodent brought you a postcard from its grand adventure. This little rodent traveled an awfully long way!

> Times Up, RODENT!
> Anyone seen a rodent around here? Goes by the name RODENT? Let 'em know that Don Landgraab is looking for a payment, if they want to continue to feel "protected".

## Project notes
Runtime injection rather than OBJK editing. (when loading into world only)
-- compatible with default replacements 

**Stages of development (testing steps):**
* Have rodent disappear and reapper and deliver flavorful notifications (no actual action happening)
* Rodents reappearing will always bring a post card
    * Requires post card mesh/model; and the board also
        * 3d model(s) and texture from s4 could be used, but post cards themselves should ideally be unique to sims 3 !

    * Post cards correspond to one of the worlds incld. vacation and normal worlds. 
    * Preventing (ideally) the same post card appearing in a row

* Rodents reappearing will have a chance to bring back a post card. Otherwise chance for artifact/treasure/collectible (variable based on EPs)
    * They will always bring something back, with a high chance of it being post card as part of the core system. But smaller chance of other. 
* Small details;
    * Adjusting textures to fit sims 3 better
    * Adding a note on the cage like in sims 4
    * adding more rat variants!


**TODOs**
- Add localization support
- Add tunability 

**Consideration**
- Ability to control whether rodent will go on adventure (can/cannot)
- Ability to send rodent on adventure (as interaction)