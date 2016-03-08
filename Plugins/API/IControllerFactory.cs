using Stormancer.Core;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Stormancer.Plugins;
using Stormancer;
using System.Linq.Expressions;
using Stormancer.Diagnostics;

namespace Server.Plugins.API
{
    public interface IControllerFactory
    {
        void RegisterControllers();
    }

    public class ControllerFactory<T> : IControllerFactory where T : ControllerBase
    {
        private ISceneHost _scene;

        public ControllerFactory(ISceneHost scene)
        {
            _scene = scene;

        }

        private async Task ExecuteRpcAction(RequestContext<IScenePeerClient> ctx, Func<T, RequestContext<IScenePeerClient>, Task> action, string route)
        {
            using (var scope = _scene.DependencyResolver.CreateChild("request"))
            {
                var controller = scope.Resolve<T>();
                controller.Request = ctx;
                try
                {
                    await action(controller, ctx);
                }
                catch (Exception ex)
                {
                    if (!await controller.HandleException(new ApiExceptionContext(route, ex, ctx)))
                    {
                        throw;
                    }
                }
            }
        }

        private void ExecuteRouteAction(Packet<IScenePeerClient> packet, Action<T, Packet<IScenePeerClient>> action, string route)
        {
            using (var scope = _scene.DependencyResolver.CreateChild("request"))
            {
                var controller = scope.Resolve<T>();
                //controller.Request = ctx;

                try
                {
                    action(controller, packet);
                }
                catch (Exception ex)
                {
                    controller.HandleException(new ApiExceptionContext(route, ex, packet)).ContinueWith(task =>
                    {
                        if (!task.IsFaulted && !task.IsCanceled)
                        {
                            if(!task.Result)
                            {
                                scope.Resolve<ILogger>().Log(LogLevel.Error, route, $"An exception occurred while executing action '{route}' in controller '{controller.GetType().Name}'.", ex);
                            }
                        }
                    });
                }
            }
        }

        public void RegisterControllers()
        {
            var type = typeof(T);

            foreach (var method in type.GetMethods())
            {
                var procedureName = GetProcedureName(type, method);
                if (IsRawAction(method))
                {

                    var ctxParam = Expression.Parameter(typeof(RequestContext<IScenePeerClient>), "ctx");
                    var controllerParam = Expression.Parameter(typeof(T), "controller");
                    var callExpr = Expression.Call(controllerParam, method, ctxParam);

                    var action = Expression.Lambda<Func<T, RequestContext<IScenePeerClient>, Task>>(callExpr, controllerParam, ctxParam).Compile();
                    _scene.AddProcedure(procedureName, ctx => ExecuteRpcAction(ctx, action, procedureName));
                }
                else if (IsRawRoute(method))
                {
                    var ctxParam = Expression.Parameter(typeof(Packet<IScenePeerClient>), "ctx");
                    var controllerParam = Expression.Parameter(typeof(T), "controller");
                    var callExpr = Expression.Call(controllerParam, method, ctxParam);

                    var action = Expression.Lambda<Action<T, Packet<IScenePeerClient>>>(callExpr, controllerParam, ctxParam).Compile();
                    _scene.AddRoute(procedureName, packet => ExecuteRouteAction(packet, action, procedureName));
                }
            }

        }

        private bool IsRawAction(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                return false;
            }

            return method.ReturnType.IsAssignableFrom(typeof(Task)) && parameters[0].ParameterType == typeof(RequestContext<IScenePeerClient>);
        }
        private bool IsRawRoute(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                return false;
            }
            return method.ReturnType == typeof(void) && parameters[0].ParameterType == typeof(Packet<IScenePeerClient>);
        }

        private string GetProcedureName(Type controller, MethodInfo method)
        {
            var root = controller.Name.ToLowerInvariant();
            if (root.EndsWith("controller"))
            {
                root = root.Substring(0, root.Length - "Controller".Length);
            }
            return (root + "." + method.Name).ToLowerInvariant();
        }
    }
}