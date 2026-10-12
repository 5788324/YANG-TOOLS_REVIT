using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YangTools.Revit.Mcp
{
    // RC1: Stateful IronPython adapter; only invoked inside the Revit ExternalEvent callback.
    internal sealed class PythonToolHandler
    {
        private readonly Action<string> _recordInitializationError;
        private ScriptEngine? _pythonEngine;
        private bool _pythonInitializationAttempted;
        private string _pythonError = string.Empty;

        internal PythonToolHandler(Action<string> recordInitializationError)
        {
            _recordInitializationError = recordInitializationError ?? throw new ArgumentNullException(nameof(recordInitializationError));
        }

        internal bool PythonInitialized => _pythonInitializationAttempted;
        internal bool PythonAvailable => _pythonEngine != null;
        internal string PythonError => _pythonError;

        internal RevitExecutionResult ExecutePython(UIApplication uiapp, JObject args)
        {
            string script = args["script"]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(script)) return Failure("Python 脚本为空。 ");
            ScriptEngine? pythonEngine = EnsurePythonEngine();
            if (pythonEngine == null) return Failure("IronPython 引擎不可用：" + _pythonError);
            Document? document = uiapp.ActiveUIDocument?.Document;
            if (document == null) return Failure("当前没有活动项目。 ");

            using (var group = new TransactionGroup(document, "YangTools MCP Python"))
            {
                group.Start();
                try
                {
                    ScriptScope scope = pythonEngine.CreateScope();
                    pythonEngine.Runtime.LoadAssembly(typeof(Document).Assembly);
                    pythonEngine.Runtime.LoadAssembly(typeof(UIApplication).Assembly);
                    scope.SetVariable("uiapp", uiapp);
                    scope.SetVariable("app", uiapp.Application);
                    scope.SetVariable("uidoc", uiapp.ActiveUIDocument);
                    scope.SetVariable("doc", document);
                    scope.SetVariable("__revit__", uiapp);
                    scope.SetVariable("__uidoc__", uiapp.ActiveUIDocument);
                    scope.SetVariable("__doc__", document);

                    using (var stream = new MemoryStream())
                    using (var writer = new StreamWriter(stream))
                    {
                        pythonEngine.Runtime.IO.SetOutput(stream, writer);
                        pythonEngine.Execute(script, scope);
                        writer.Flush();
                        stream.Position = 0;
                        string output = new StreamReader(stream).ReadToEnd();
                        string value = scope.TryGetVariable("__result__", out object? pythonResult)
                            ? pythonResult?.ToString() ?? string.Empty
                            : string.Empty;
                        group.Assimilate();
                        return Success(JsonConvert.SerializeObject(new
                        {
                            output,
                            result = value,
                            transactionGroup = group.GetStatus().ToString()
                        }));
                    }
                }
                catch (Exception ex)
                {
                    if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
                    return Failure(ex.Message);
                }
            }
        }


        private ScriptEngine? EnsurePythonEngine()
        {
            if (_pythonInitializationAttempted) return _pythonEngine;
            _pythonInitializationAttempted = true;
            try
            {
                _pythonEngine = Python.CreateEngine();
                _pythonError = string.Empty;
            }
            catch (Exception ex)
            {
                _pythonEngine = null;
                _pythonError = ex.Message;
                _recordInitializationError("IronPython 初始化失败：" + ex.Message);
            }
            return _pythonEngine;
        }

        private static RevitExecutionResult Success(string result) => new RevitExecutionResult { Success = true, Result = result ?? string.Empty };
        private static RevitExecutionResult Failure(string error) => new RevitExecutionResult { Success = false, Error = error ?? string.Empty };
    }
}
