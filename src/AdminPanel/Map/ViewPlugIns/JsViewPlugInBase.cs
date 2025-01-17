﻿// <copyright file="JsViewPlugInBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.AdminPanel.Map.ViewPlugIns;

using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>
/// Base class for a javascript map view plugin.
/// </summary>
public abstract class JsViewPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsViewPlugInBase"/> class.
    /// </summary>
    /// <param name="jsRuntime">The js runtime.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="jsMethodName">Name of the js method.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected JsViewPlugInBase(IJSRuntime jsRuntime, ILoggerFactory loggerFactory, string jsMethodName, CancellationToken cancellationToken)
    {
        this.JsRuntime = jsRuntime;
        this.JsMethodName = jsMethodName;
        this.CancellationToken = cancellationToken;
        this.Logger = loggerFactory.CreateLogger(this.GetType());
    }

    /// <summary>
    /// Gets the logger for this class.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the <see cref="IJSRuntime" /> to call the <see cref="JsMethodName" />.
    /// </summary>
    protected IJSRuntime JsRuntime { get; }

    /// <summary>
    /// Gets the method name of the javascript function which implements this view plugin logic on the client side.
    /// </summary>
    protected string JsMethodName { get; }

    /// <summary>
    /// Gets the <see cref="CancellationToken" /> which stops calling the <see cref="JsMethodName" /> after the map object has been removed.
    /// </summary>
    protected CancellationToken CancellationToken { get; }

    /// <summary>
    /// Invokes the <see cref="JsMethodName" /> with the specified parameters.
    /// </summary>
    /// <param name="args">The parameters for the function call.</param>
    /// <returns>The <see cref="ValueTask"/> of this async operation.</returns>
    protected async ValueTask InvokeAsync(params object[] args)
    {
        const int maximumRetries = 10;
        var tryAgain = true;
        for (int i = 0; i < maximumRetries && tryAgain && !this.CancellationToken.IsCancellationRequested; i++)
        {
            try
            {
                await this.JsRuntime.InvokeVoidAsync(this.JsMethodName, this.CancellationToken, args);
                tryAgain = false;
            }
            catch (TaskCanceledException)
            {
                // don't need to handle that.
                tryAgain = false;
            }
            catch (JSException e)
                when (e.Message.StartsWith("Could not find '") && (e.Message.Contains("' in 'window'.") || e.Message.Contains("' was undefined).")))
            {
                // In this case, try again in a moment.
                await Task.Delay(500, this.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                tryAgain = false;
                this.Logger.LogError(e, $"Error in {this.GetType().Name}; params: {string.Join(';', args)}");
            }
        }
    }
}