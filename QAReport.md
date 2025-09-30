# QA Report
Use this file to outline the test strategy for this package.

## QA Owner: 
Darren Quek, Ed Shih

## Test strategy

2D Animation Test Cases in Test Rail:
https://qatestrail.hq.unity3d.com/index.php?/suites/view/2067&group_by=cases:section_id&group_order=asc&group_id=30244

Jira Tracker:
https://unity3d.atlassian.net/browse/UNI-366

## Package Status

Did new test coverage for v1 fixes by Sergi.

The package was tested with existing project: 2D animation Samples.

2 Main Issues fixed:
 1. Case: 1092096-Prefabs with SpriteSkin loses references to bone hierarchy when Library folder is rebuilt/different
 2. Forum Reported:
    "The scene viewport however shows the character without any bones applied, but some dotted lines in place of the bone connections."

New V2 Package 

Main test coverage for new UI for the Animation Skinning Editor. 
As well as coverage of it working in Character mode working with PSD importer.
V2 Samples are also checked with the latest package.

New V2.1 Package 

Tests ran for ECS Burst optimizations as well as bug fixes to the tool. Copy/Paste Bug Fixes.
V2 Samples are also checked with the latest package.

Issues fixed:
 1. Copy/Paste Bug Fixes:
     - Checkbox for pasting not working
     - When user paste individual mesh, bones will follow


/////////////////////////////////////////////

Package is stable with no reported/unresolved crashes, and no major unresolved bugs.

Level 1-2 testing completed for Animation phase 1 (single sprite animation). Worked on project with Montreal animators to get feedback.

Level 1-2 testing completed for Animation phase 2 and 2.1 (PSB Character animation). 

### Known bugs, issues:
There are no critical bugs/issues that would impede package release.
Refer to the FogBugz filter for a full list of issues.

FogBugz Tag Filter for issues:
https://fogbugz.unity3d.com/f/search/?sSearchFor=tag%3A%222d-animation%22


* performance metrics to be added