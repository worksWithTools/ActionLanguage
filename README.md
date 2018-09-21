# ActionLanguage
Action Language as used by EDD

It includes the following projects:

* ActionLanguage - Package newtonsoft.json, 

** Project https://github.com/EDDiscovery/BaseUtilities / BaseUtils + Audio, 

** Project https://github.com/EDDiscovery/ExtendedControls

* Extended Forms - no package dependencies, project https://github.com/EDDiscovery/BaseUtilities / BaseUtils + Audio, Extended Controls (relative ref)

The vsproj has had its ProjectReference for Baseutilities manually changed to use $(SolutionDir) as the base folder to find Baseutilities. So it expects Baseutilities to be checked out at c:\code\parent\Baseutilities\..

Check this module out and you can use the TestExtendedControls forms to test this extended controls

Note when you include this module in a parent module, you should not expand the baseutilities submodule.  Instead include it in the parent module itself.  

