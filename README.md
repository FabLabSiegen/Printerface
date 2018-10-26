# Printerface
the bare C# Adapter for adding Octoprint to any given Project that supports C#, e.g. Godot Gameengine, xenko Gameengine or Unity3D
This Interface is mainly built uppon http://docs.octoprint.org/en/master/api/index.html

Usage:
  include the Release dll in your C# project and Create an Octoprintconnection with the ip-address or domainname and an API key of your octoprint server

given ip address 192.168.0.2 and api key 123456xyz you create it with
  OctoprintConnection connection;
  connection = new OctoprintConnection(http://192.168.0.2/,123456xyz);

you can then find commands to request information or do something with the printer through the OctoprintConnection's references to the other classes:
  OctoprintPosTracker Position (guessing position of the printhead from gcode or other data and issuing commands changing positions)
  OctoprintFileTracker Files (based on http://docs.octoprint.org/en/master/api/files.html)
  OctoprintJobTracker Jobs (based on http://docs.octoprint.org/en/master/api/job.html)
  OctoprintPrinterTracker Printers (based on http://docs.octoprint.org/en/master/api/printer.html)
for the Position's async tracking capabilities you can start the secondary thread, that automatically updates the position every 10 seconds and guesses in realtime from gcode position inbetween for latency issues with connection.Positions.StartThread()

This Project was done in a University so if you use it, it would be awesome to hear from you since Research into Human Computer Interaction is our daily Job and Information on how People use our Tech  is our main Currency ^^
