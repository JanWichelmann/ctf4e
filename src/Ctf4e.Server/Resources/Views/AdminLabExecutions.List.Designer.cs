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
    internal class AdminLabExecutions_List {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal AdminLabExecutions_List() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Ctf4e.Server.Resources.Views.AdminLabExecutions.List", typeof(AdminLabExecutions_List).Assembly);
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
        ///   Looks up a localized string similar to Actions.
        /// </summary>
        internal static string Actions {
            get {
                return ResourceManager.GetString("Actions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cancel.
        /// </summary>
        internal static string Cancel {
            get {
                return ResourceManager.GetString("Cancel", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Delete.
        /// </summary>
        internal static string Delete {
            get {
                return ResourceManager.GetString("Delete", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Edit.
        /// </summary>
        internal static string Edit {
            get {
                return ResourceManager.GetString("Edit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;p&gt;
        ///    Lab executions control when a lab starts and ends. The following rules apply:
        ///&lt;/p&gt;
        ///&lt;ul&gt;
        ///    &lt;li&gt;&lt;strong&gt;Preparatory phase:&lt;/strong&gt; At this point, certain exercises can be solved and all flags can be submitted.&lt;/li&gt;
        ///    &lt;li&gt;&lt;strong&gt;Begin:&lt;/strong&gt; All exercises can be solved and all flags can be submitted. Participants can pass the lab.&lt;/li&gt;
        ///    &lt;li&gt;&lt;strong&gt;End:&lt;/strong&gt; Participants can still solve exercises and submit flags, but those no longer influence the grade or the scoreboard.&lt;/li&gt;
        ///&lt;/ [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string Explanation {
            get {
                return ResourceManager.GetString("Explanation", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Begin.
        /// </summary>
        internal static string LabExecutions_Begin {
            get {
                return ResourceManager.GetString("LabExecutions:Begin", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to End.
        /// </summary>
        internal static string LabExecutions_End {
            get {
                return ResourceManager.GetString("LabExecutions:End", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Group.
        /// </summary>
        internal static string LabExecutions_Group {
            get {
                return ResourceManager.GetString("LabExecutions:Group", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Lab.
        /// </summary>
        internal static string LabExecutions_Lab {
            get {
                return ResourceManager.GetString("LabExecutions:Lab", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Preparatory phase.
        /// </summary>
        internal static string LabExecutions_PreStart {
            get {
                return ResourceManager.GetString("LabExecutions:PreStart", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;p&gt;Do you really want to delete the execution of lab &amp;quot;&lt;span data-replace=&quot;lab-name&quot;&gt;&lt;/span&gt;&amp;quot; for group &amp;quot;&lt;span data-replace=&quot;group-name&quot;&gt;&lt;/span&gt;&amp;quot;?&lt;/p&gt;&lt;p&gt;The group will no longer be able to submit exercises and flags, and existing submissions will be ignored.&lt;/p&gt;.
        /// </summary>
        internal static string Modal_DeleteLabExecutionGroup_Body {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionGroup:Body", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Delete lab execution.
        /// </summary>
        internal static string Modal_DeleteLabExecutionGroup_Submit {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionGroup:Submit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Delete lab execution for group.
        /// </summary>
        internal static string Modal_DeleteLabExecutionGroup_Title {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionGroup:Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;p&gt;Note: The groups will no longer be able to submit exercises, and all existing submissions will be ignored.&lt;/p&gt;.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Body {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Body", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Lab.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Form_Lab {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Form:Lab", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Select lab.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Form_Lab_Placeholder {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Form:Lab:Placeholder", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Slot.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Form_Slot {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Form:Slot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Select slot.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Form_Slot_Placeholder {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Form:Slot:Placeholder", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Delete lab executions.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Submit {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Submit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Delete all lab executions for slot.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Title {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Start lab for one group.
        /// </summary>
        internal static string StartForOneGroup {
            get {
                return ResourceManager.GetString("StartForOneGroup", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Start lab for all groups of a slot.
        /// </summary>
        internal static string StartForSlot {
            get {
                return ResourceManager.GetString("StartForSlot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Stop lab for all groups of a slot.
        /// </summary>
        internal static string StopForSlot {
            get {
                return ResourceManager.GetString("StopForSlot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Manage lab executions.
        /// </summary>
        internal static string Title {
            get {
                return ResourceManager.GetString("Title", resourceCulture);
            }
        }
    }
}