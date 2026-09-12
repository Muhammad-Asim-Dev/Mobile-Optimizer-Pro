# Internal optimizer regression tests

This directory is development/QA infrastructure and is outside `Assets/MobilePerformanceOptimizer`. Exclude it from the Asset Store package.

Requirements: Unity 6, URP, Unity Test Framework. Save existing scene content before running tests interactively. Fixtures create uniquely named temporary asset folders, restore captured project settings and the existing optimizer restore-session file, and clean up their assets. They refuse to save user-created untitled scene content.

Run all EditMode tests with Window → General → Test Runner, or use Unity’s batch runner:

```text
Unity.exe -batchmode -nographics -projectPath <isolated-project> -runTests -testPlatform EditMode -testResults <results.xml> -logFile <editor.log>
```

To rerun only this suite, add `-testFilter MPOApplyRegressionTests`.

For this change, validation used `.utmp/UnityValidation` with the installed Unity 6000.0.68f1 and URP 17.0.4. Only the optimizer and internal test assets were copied there. Original project assets, scenes and settings were not used as test fixtures. The disposable project has a `.mpo-isolated-validation` marker containing `Mobile Performance Optimizer isolated regression project`; only in an explicitly marked project does the one-time setup save Unity�s generated startup scene before testing. Do not put this marker in a working customer project.

The latest complete EditMode run passed 22/22 cases. Final rerun results are recorded in `.utmp/optimizer-regressions-final.xml` at the repository root; the Editor log is adjacent. Earlier failing-run artifacts were retained for diagnosis, and failures were fixed rather than suppressed.
