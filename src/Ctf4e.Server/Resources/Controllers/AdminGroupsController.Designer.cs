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
    internal class AdminGroupsController {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal AdminGroupsController() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Ctf4e.Server.Resources.Controllers.AdminGroupsController", typeof(AdminGroupsController).Assembly);
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
        internal static string CreateGroupAsync_InvalidInput {
            get {
                return ResourceManager.GetString("CreateGroupAsync:InvalidInput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The group was created successfully..
        /// </summary>
        internal static string CreateGroupAsync_Success {
            get {
                return ResourceManager.GetString("CreateGroupAsync:Success", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An error occured while creating the group. Check the log for more details..
        /// </summary>
        internal static string CreateGroupAsync_UnknownError {
            get {
                return ResourceManager.GetString("CreateGroupAsync:UnknownError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The group needs to be empty before it can be deleted..
        /// </summary>
        internal static string DeleteGroupAsync_GroupNotEmpty {
            get {
                return ResourceManager.GetString("DeleteGroupAsync:GroupNotEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This group does not exist..
        /// </summary>
        internal static string DeleteGroupAsync_NotFound {
            get {
                return ResourceManager.GetString("DeleteGroupAsync:NotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The group was deleted successfully..
        /// </summary>
        internal static string DeleteGroupAsync_Success {
            get {
                return ResourceManager.GetString("DeleteGroupAsync:Success", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An error occured while deleting the group. Check the log for more details..
        /// </summary>
        internal static string DeleteGroupAsync_UnknownError {
            get {
                return ResourceManager.GetString("DeleteGroupAsync:UnknownError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid input..
        /// </summary>
        internal static string EditGroupAsync_InvalidInput {
            get {
                return ResourceManager.GetString("EditGroupAsync:InvalidInput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The group was updated successfully..
        /// </summary>
        internal static string EditGroupAsync_Success {
            get {
                return ResourceManager.GetString("EditGroupAsync:Success", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An error occured while updating the group. Check the log for more details..
        /// </summary>
        internal static string EditGroupAsync_UnknownError {
            get {
                return ResourceManager.GetString("EditGroupAsync:UnknownError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No group specified..
        /// </summary>
        internal static string ShowEditGroupFormAsync_MissingParameter {
            get {
                return ResourceManager.GetString("ShowEditGroupFormAsync:MissingParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This group does not exist..
        /// </summary>
        internal static string ShowEditGroupFormAsync_NotFound {
            get {
                return ResourceManager.GetString("ShowEditGroupFormAsync:NotFound", resourceCulture);
            }
        }
    }
}
