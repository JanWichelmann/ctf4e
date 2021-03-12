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
    internal class AdminLabsController {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal AdminLabsController() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Ctf4e.Server.Resources.Controllers.AdminLabsController", typeof(AdminLabsController).Assembly);
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
        ///   Looks up a localized string similar to Invalid input..
        /// </summary>
        internal static string CreateLabAsync_InvalidInput {
            get {
                return ResourceManager.GetString("CreateLabAsync:InvalidInput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The lab was created successfully..
        /// </summary>
        internal static string CreateLabAsync_Success {
            get {
                return ResourceManager.GetString("CreateLabAsync:Success", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An error occured when creating the lab. Check the log for more details..
        /// </summary>
        internal static string CreateLabAsync_UnknownError {
            get {
                return ResourceManager.GetString("CreateLabAsync:UnknownError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The lab has at least one execution and thus cannot be deleted anymore..
        /// </summary>
        internal static string DeleteLabAsync_HasExecutions {
            get {
                return ResourceManager.GetString("DeleteLabAsync:HasExecutions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This lab does not exist..
        /// </summary>
        internal static string DeleteLabAsync_NotFound {
            get {
                return ResourceManager.GetString("DeleteLabAsync:NotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The lab was deleted successfully..
        /// </summary>
        internal static string DeleteLabAsync_Success {
            get {
                return ResourceManager.GetString("DeleteLabAsync:Success", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An error occured when deleting the lab. Check the log for more details..
        /// </summary>
        internal static string DeleteLabAsync_UnknownError {
            get {
                return ResourceManager.GetString("DeleteLabAsync:UnknownError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid input..
        /// </summary>
        internal static string EditLabAsync_InvalidInput {
            get {
                return ResourceManager.GetString("EditLabAsync:InvalidInput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The lab was updated successfully..
        /// </summary>
        internal static string EditLabAsync_Success {
            get {
                return ResourceManager.GetString("EditLabAsync:Success", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An error occured when updating the lab. Check the log for more details..
        /// </summary>
        internal static string EditLabAsync_UnknownError {
            get {
                return ResourceManager.GetString("EditLabAsync:UnknownError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No lab specified..
        /// </summary>
        internal static string ShowEditLabFormAsync_MissingParameter {
            get {
                return ResourceManager.GetString("ShowEditLabFormAsync:MissingParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This lab does not exist..
        /// </summary>
        internal static string ShowEditLabFormAsync_NotFound {
            get {
                return ResourceManager.GetString("ShowEditLabFormAsync:NotFound", resourceCulture);
            }
        }
    }
}
