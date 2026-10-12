using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YangTools.Revit.Mcp
{
    // RC1: Existing wire commands are pinned to domain handlers; no changes to the MCP Gateway contract.
    internal sealed class RevitToolRouter
    {
        private readonly PythonToolHandler _python;

        internal RevitToolRouter(PythonToolHandler python)
        {
            _python = python ?? throw new ArgumentNullException(nameof(python));
        }

        internal RevitExecutionResult Dispatch(UIApplication uiapp, string command, JObject args)
        {
            switch (command)
            {
                case "get_revit_status":
                    return Success(CoreDocumentToolHandler.BuildRevitStatus(uiapp).ToString(Formatting.None));
                case "get_active_document":
                    return Success(CoreDocumentToolHandler.BuildActiveDocument(uiapp).ToString(Formatting.None));
                case "get_selection":
                    return Success(CoreDocumentToolHandler.BuildSelection(uiapp).ToString(Formatting.None));
                case "get_element_parameters":
                    return Success(CoreDocumentToolHandler.BuildElementParameters(uiapp, args).ToString(Formatting.None));
                case "query_elements":
                    return Success(CoreDocumentToolHandler.QueryElements(uiapp, args).ToString(Formatting.None));
                case "inspect_rebar":
                    return RebarToolHandler.InspectRebar(uiapp, args);
                case "list_rebar_types":
                    return RebarToolHandler.ListRebarTypes(uiapp);
                case "create_shape_rebar":
                    return RebarToolHandler.CreateShapeRebar(uiapp, args);
                case "inspect_rebar_host_faces":
                    return RebarToolHandler.InspectRebarHostFaces(uiapp, args);
                case "create_shape_rebar_on_face":
                    return RebarToolHandler.CreateShapeRebarOnFace(uiapp, args);
                case "create_shape_rebar_varying_on_face":
                    return RebarToolHandler.CreateShapeRebarVaryingOnFace(uiapp, args);
                case "modify_shape_rebar":
                    return RebarToolHandler.ModifyShapeRebar(uiapp, args);
                case "rebind_shape_rebar_constraints":
                    return RebarToolHandler.RebindShapeRebarConstraints(uiapp, args);
                case "list_rebar_hook_types":
                    return RebarToolHandler.ListRebarHookTypes(uiapp);
                case "constrain_rebar_to_rebar":
                    return RebarToolHandler.ConstrainRebarToRebar(uiapp, args);
                case "modify_rebar_hooks":
                    return RebarToolHandler.ModifyRebarHooks(uiapp, args);
                case "create_free_form_rebar":
                    return RebarToolHandler.CreateFreeFormRebar(uiapp, args);
                case "modify_free_form_rebar":
                    return RebarToolHandler.ModifyFreeFormRebar(uiapp, args);
                case "analyze_rebar_clearance":
                    return RebarToolHandler.AnalyzeRebarClearance(uiapp, args);
                case "list_rebar_coupler_types":
                    return RebarToolHandler.ListRebarCouplerTypes(uiapp);
                case "create_rebar_coupler":
                    return RebarToolHandler.CreateRebarCoupler(uiapp, args);
                case "probe_gradient_rebar_capabilities":
                    return RebarToolHandler.ProbeGradientRebarCapabilities(uiapp);
                case "preview_gradient_spacing_plan":
                    return RebarToolHandler.PreviewGradientSpacingPlan(args);
                case "probe_gradient_server_create":
                    return RebarToolHandler.ProbeGradientServerCreate(uiapp, args);
                case "preview_gradient_rebar_set":
                    return RebarToolHandler.ExecuteGradientRebarSet(uiapp, args, forceDryRun: true);
                case "create_gradient_rebar_set":
                    return RebarToolHandler.ExecuteGradientRebarSet(uiapp, args, forceDryRun: false);
                case "execute_revit_python":
                    return _python.ExecutePython(uiapp, args);
                default:
                    return Failure("不支持的 Revit 执行命令：" + command);
            }
        }

        private static RevitExecutionResult Success(string result) => new RevitExecutionResult { Success = true, Result = result ?? string.Empty };
        private static RevitExecutionResult Failure(string error) => new RevitExecutionResult { Success = false, Error = error ?? string.Empty };
    }
}
