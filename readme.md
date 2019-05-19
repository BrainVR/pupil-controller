This is a controller for Pupil labs eyetracking devices and their use within unity. 

It is built on top of [PupilLabs examples](https://github.com/pupil-labs/hmd-eyes/) and copies a lot of code and plugins used (such as FFMpeg or MessagePack). 

# Reasons to rebuild
The example code at many placese was too clunky and not convenient to implement in our projects that I decided to somewhat completely rewrite the package to higher Unity standards. I am removing a lots of static classes, implementing clearer structure to the calls etc. But the core of the code remained unchanged, I basically just cleaned it up and wrote a bunch of wrappers around :)