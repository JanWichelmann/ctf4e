﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Ctf4e.Server.Resources.Views {
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
    internal class Authentication_GroupSelection {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Authentication_GroupSelection() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Ctf4e.Server.Resources.Views.Authentication.GroupSelection", typeof(Authentication_GroupSelection).Assembly);
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
        ///   Looks up a localized string similar to Group name.
        /// </summary>
        internal static string Form_DisplayName {
            get {
                return ResourceManager.GetString("Form:DisplayName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This field has full Unicode support, so apart from the length limitation there are no limits to your creativity. Afterwards, the group name cannot be changed anymore..
        /// </summary>
        internal static string Form_DisplayName_Description {
            get {
                return ResourceManager.GetString("Form:DisplayName:Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Done? Great! Please check all inputs once again. If everything looks fine, you can click &quot;Create group&quot;. The system will then create a new group and redirect you to the lab dashboard..
        /// </summary>
        internal static string Form_FinalText {
            get {
                return ResourceManager.GetString("Form:FinalText", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Codes of your group partners.
        /// </summary>
        internal static string Form_OtherUserCodes {
            get {
                return ResourceManager.GetString("Form:OtherUserCodes", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to One code per line..
        /// </summary>
        internal static string Form_OtherUserCodes_Description {
            get {
                return ResourceManager.GetString("Form:OtherUserCodes:Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Participate in CTF and show group in scoreboard.
        /// </summary>
        internal static string Form_ShowInScoreboard {
            get {
                return ResourceManager.GetString("Form:ShowInScoreboard", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If you check this field, you participate in the course&apos;s Capture-The-Flag competition, and the name of your group will be shown in the scoreboard. Participation in the CTF is optional and has no disadvantages. The scoreboard only shows the top scores..
        /// </summary>
        internal static string Form_ShowInScoreboard_Description {
            get {
                return ResourceManager.GetString("Form:ShowInScoreboard:Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Time slot.
        /// </summary>
        internal static string Form_SlotId {
            get {
                return ResourceManager.GetString("Form:SlotId", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This must match the time slot which has been assigned to you..
        /// </summary>
        internal static string Form_SlotId_Description {
            get {
                return ResourceManager.GetString("Form:SlotId:Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Create group.
        /// </summary>
        internal static string Form_Submit {
            get {
                return ResourceManager.GetString("Form:Submit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Create group.
        /// </summary>
        internal static string Title {
            get {
                return ResourceManager.GetString("Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Your code: &lt;span class=&quot;fw-bold font-monospace&quot;&gt;{0}&lt;/span&gt;.
        /// </summary>
        internal static string YourCode {
            get {
                return ResourceManager.GetString("YourCode", resourceCulture);
            }
        }
    }
}
