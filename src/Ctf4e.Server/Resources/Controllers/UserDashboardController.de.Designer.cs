﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Ctf4e.Server.Resources.Controllers {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class UserDashboardController_de {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal UserDashboardController_de() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Ctf4e.Server.Resources.Controllers.UserDashboardController.de", typeof(UserDashboardController_de).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Dieses Praktikum ist nicht aktiv..
        /// </summary>
        internal static string CallLabServerAsync_LabNotActive {
            get {
                return ResourceManager.GetString("CallLabServerAsync:LabNotActive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Dieses Praktikum existiert nicht..
        /// </summary>
        internal static string CallLabServerAsync_LabNotFound {
            get {
                return ResourceManager.GetString("CallLabServerAsync:LabNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Sie sind nicht angemeldet oder keiner Gruppe zugewiesen..
        /// </summary>
        internal static string CallLabServerAsync_NoGroup {
            get {
                return ResourceManager.GetString("CallLabServerAsync:NoGroup", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Die Übersicht für Praktikum #{0} konnte nicht abgerufen werden..
        /// </summary>
        internal static string RenderAsync_EmptyScoreboard {
            get {
                return ResourceManager.GetString("RenderAsync:EmptyScoreboard", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Dieses Praktikum existiert nicht..
        /// </summary>
        internal static string RenderAsync_LabNotFound {
            get {
                return ResourceManager.GetString("RenderAsync:LabNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Aktuell ist kein Praktikum aktiv..
        /// </summary>
        internal static string RenderAsync_NoActiveLab {
            get {
                return ResourceManager.GetString("RenderAsync:NoActiveLab", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Sie sind nicht angemeldet oder keiner Gruppe zugewiesen..
        /// </summary>
        internal static string RenderAsync_NoGroup {
            get {
                return ResourceManager.GetString("RenderAsync:NoGroup", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Die Flag konnte nicht eingelöst werden..
        /// </summary>
        internal static string SubmitFlagAsync_Error {
            get {
                return ResourceManager.GetString("SubmitFlagAsync:Error", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Sie sind nicht angemeldet oder keiner Gruppe zugewiesen..
        /// </summary>
        internal static string SubmitFlagAsync_NoGroup {
            get {
                return ResourceManager.GetString("SubmitFlagAsync:NoGroup", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Einlösen der Flag erfolgreich!.
        /// </summary>
        internal static string SubmitFlagAsync_Success {
            get {
                return ResourceManager.GetString("SubmitFlagAsync:Success", resourceCulture);
            }
        }
    }
}
