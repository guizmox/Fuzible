using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.DirectoryServices;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;

namespace FuzibleFramework
{
    [ComImport, Guid("9068270b-0939-11d1-8be1-00c04fd8d503"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    internal interface IAdsLargeInteger
    {
        long HighPart
        {
            [SuppressUnmanagedCodeSecurity]
            get; [SuppressUnmanagedCodeSecurity]
            set;
        }

        long LowPart
        {
            [SuppressUnmanagedCodeSecurity]
            get; [SuppressUnmanagedCodeSecurity]
            set;
        }
    }

    public class ADTools
    {
        #region "CONSTANTES"
        public static readonly List<string> AD_OBJECTS = new() { "groups", "users" };
        //public static List<string> AD_FIELDS_GROUPS = new List<string> { "iscriticalsystemobject", "usnchanged", "distinguishedname", "grouptype", "whencreated", 
        //    "samaccountname", "objectsid", "instancetype", "adspath", "usncreated", "whenchanged", "cn", 
        //    "samaccounttype", "objectguid", "objectcategory", "description", "objectclass",
        //    "dscorepropagationdata", "name" };
        public static readonly List<string> AD_FIELDS_GROUPS = new()
        { "adminDescription","adminDisplayName","ADsPath","authOrig",
                                                                        "authOrigBL","canonicalName","Class","cn",
                                                                        "createTimeStamp","delivContLength","descritpion","displayName",
                                                                        "displayNamePrintable","distinguishedName","dLMemRejectPerms","dLMemRejectPermsBL",
                                                                        "dLMemSubmitPerms","dLMemSubmitPermsBL","extensionAttribute","groupType",
                                                                        "homeMTA","info","isDeleted","legacyExchangeDN",
                                                                        "mail","mailNickName","managedBy","member",
                                                                        "memberOf","modifyTimeStamp","msExchExpansionServerName","msExchHideFromAddressLists",
                                                                        "msExchHomeServerName","msExchRequireAuthToSendTo","msSFU30GidNumber","msSFU30Name",
                                                                        "msSFU30NisDomain","msSFU30PosixMember","name",
                                                                        "nTSecurityDescriptor","objectCategory","objectClass","objectGUID",
                                                                        "objectSid","oOFReplyToOriginator","Parent","primaryGroupToken",
                                                                        "proxyAddresses","reportToOriginator","reportToOwner","sAMAccountName",
                                                                        "telephoneNumber","textEncodedORAddress","unauthOrig","unauthOrigBL",
                                                                        "uSNChanged","uSNCreated","whenChanged","whenCreated"};
        public static List<string> AD_FIELDS_USERS = new()
        { "distinguishedname", "setpassword", "badpasswordtime","admincount","whencreated","whenchanged","primarygroupid","msexchuserbl","memberof","msexchumdtmfmap","lockouttime","objectguid","lastlogontimestamp","iscriticalsystemobject","samaccountname","instancetype","objectclass","accountexpires","description","cn","useraccountcontrol","serviceprincipalname","samaccounttype","dscorepropagationdata","msexchalobjectversion","userprincipalname","protocolsettings","objectcategory","adspath","mstsexpiredate","usnchanged","msmqsigncertificates","usercertificate","name","codepage","objectsid","msexchwhenmailboxcreated","pwdlastset","displayname","usncreated","countrycode","mstslicenseversion","msmqdigests","mstsmanagingls","badpwdcount","msexchomaadminwirelessenable","msexchmailboxauditenable","msexchtransportrecipientsettingsflags","msexchbypassaudit","msexchrecipienttypedetails","msexchprovisioningflags","msexchversion","msexchrecipientdisplaytype","msexchmoderationflags","garbagecollperiod","msexchumenabledflags2","msexchuseraccountcontrol","msexchmailboxauditlogagelimit","msexchrecipientsoftdeletedstatus","internetencoding","msexchaddressbookflags","msexchmdbrulesquota","mail","msexchhidefromaddresslists","targetaddress","msexcharchivequota","msexchdumpsterquota","msexchpoliciesincluded","mailnickname","msexcharchivewarnquota","textencodedoraddress","msexchdumpsterwarningquota","legacyexchangedn","msexchcalendarloggingquota","proxyaddresses","msexchtextmessagingstate","msexchoabgeneratingmailboxbl","msexchcapabilityidentifiers","msexchmailboxguid","submissioncontlength","msexchmailboxtemplatelink","msexchelcmailboxflags","msexchapprovalapplicationlink","msexchhomeservername","msexchmasteraccountsid","mdbusedefaults","msexchrequireauthtosendto","msexchmailboxsecuritydescriptor","homemdb","sn","comment","msnpallowdialin","userparameters","logonhours","ms-ds-consistencyguid","logoncount","lastlogon","msds-supportedencryptiontypes","lastlogoff","showinadvancedviewonly","givenname","msds-keycredentiallink","mstslicenseversion2","msds-externaldirectoryobjectid","mstslicenseversion3","msexchpreviousrecipienttypedetails","managedobjects","physicaldeliveryofficename","msexchcomanagedobjectsbl","msrtcsip-internetaccessenabled","msexchowapolicy","department","msrtcsip-federationenabled","msexchremoterecipienttype","mobile","msexchsaferecipientshash","title","msrtcsip-optionflags","directreports","l","msexchmobilemailboxflags","msexchblockedsendershash","msexchsafesendershash","msrtcsip-deploymentlocator","manager","thumbnailphoto","msexchmobilemailboxpolicylink","extensionattribute9","telephonenumber","msrtcsip-primaryhomeserver","msrtcsip-userenabled","company","extensionattribute15","showinaddressbook","msrtcsip-primaryuseraddress","msrtcsip-userpolicies","msexchuserholdpolicies","msrtcsip-line","msexcharchivename","msexcharchivestatus","homephone","msexcharchiveguid","info","publicdelegatesbl","msexchsharingpartneridentities","msexchdelegatelistbl","msexchpoliciesexcluded","deliverandredirect","authorigbl","msexchshadowproxyaddresses","msexchdelegatelistlink","publicdelegates","msexchdisabledarchiveguid","msexchuserculture","msexchrbacpolicylink","extensionattribute1","msexchshadowmailnickname","msexchshadowdisplayname","authorig","mdboverquotalimit","mdboverhardquotalimit","employeeid","msexchrmscomputeraccountslink","msexchmessagehygienescldeletethreshold","mdbstoragequota","msexchmessagehygienesclquarantinethreshold","msexchmessagehygienescljunkthreshold","msexchmessagehygienesclrejectthreshold","altrecipient","msds-lastknownrdn","co","c","msexchshadowcountrycode","msexchshadowgivenname","ipphone","msexchgroupsecurityflags","msexchmailboxfolderset","msexcharchivedatabaselink","employeenumber","pager","lastknownparent","msexchprevioushomemdb","initials" };
        #endregion

        #region "VARIABLES"
        public SQLTools_Enums.CLASS_PURPOSE ClassPurpose { get; }
        public Job JobParameters;
        public LogTools MyLog;
        public CONNString Connection;
        private bool CommitExceptionsAsErrors = true;
        private readonly string SearchProperty = "name";
        private readonly string TagProperty = "description";
        private readonly bool AddSecurityMask = false;
        private readonly SecurityMasks SecurityMask = SecurityMasks.None; //Sacl : 184 fields - Dacl : 184 fields

        #endregion

        #region "PROPRIETES"
        #endregion

        #region "PUBLIC VOID"

        public ADTools(Job INIP, SQLTools_Enums.CLASS_PURPOSE sSourceTargetLog, ref LogTools LogJob)
        {
            JobParameters = INIP;
            MyLog = LogJob;
            SearchProperty = Toolbox.ScriptLanguage.InterpretAndReplaceScriptLanguage(INIP.ADSearchProperty, INIP.DynParams);

            switch (sSourceTargetLog)
            {
                case SQLTools_Enums.CLASS_PURPOSE.SRC: //SOURCE
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.SRC;
                    Connection = JobParameters.ConnectionString_Source;
                    break;
                case SQLTools_Enums.CLASS_PURPOSE.TRG: //TARGET
                    ClassPurpose = SQLTools_Enums.CLASS_PURPOSE.TRG;
                    Connection = JobParameters.ConnectionString_Target;
                    break;
            }

        }

        public DataSet GetDataFromAD(ref Query FuzibleQuery)
        {
            DataSet dsData = new(FuzibleQuery.QueryAnalyzer.Tables[0].Alias)
            {
                CaseSensitive = false
            };

            DataTable dtMain = new();
            DataTable dtJoined = new();
            DataTable dtMemberOf = new("MemberOf");
            dtMemberOf.Columns.Add("object");
            dtMemberOf.Columns.Add("objectguid");
            dtMemberOf.Columns.Add("name");
            dtMemberOf.Columns.Add("memberof");

            int iTable = -1;

            foreach (Query.QTable qT in FuzibleQuery.QueryAnalyzer.Tables)
            {
                iTable++;

                DataTable dtResults = iTable == 0 ? dtMain : dtJoined;

                dtResults.TableName = qT.Alias;
                dtMemberOf.TableName = string.Concat("ObjectMemberOf");

                //sConn = "LDAP://DC=securit,DC=fr";
                System.DirectoryServices.DirectoryEntry adEntry = ADConnection(Connection, JobParameters.DynParams);
                DirectorySearcher adSearch = new(adEntry);

                string sADQuery = GetADQuery(Connection, qT.Name);

                adSearch.Filter = sADQuery;

                adSearch.SearchScope = JobParameters.ADSearchScope switch
                {
                    SQLTools_Enums.AD_SEARCH_SCOPE.AD_BASE => System.DirectoryServices.SearchScope.Base,
                    SQLTools_Enums.AD_SEARCH_SCOPE.AD_ONELEVEL => System.DirectoryServices.SearchScope.OneLevel,
                    SQLTools_Enums.AD_SEARCH_SCOPE.AD_SUBTREE => System.DirectoryServices.SearchScope.Subtree,
                    _ => System.DirectoryServices.SearchScope.Subtree,
                };

                SearchResultCollection mySearchResultColl = null;

                List<Query.QField> sColumns = FuzibleQuery.QueryAnalyzer.Fields.Where(f => f.Table.Equals(qT.Name)).ToList();
                List<Query.QField> sFakeColumns = FuzibleQuery.QueryAnalyzer.Fields.Where(s => (s.Name.StartsWith("'") && s.Name.EndsWith("'"))).ToList();

                //pour les utilisateurs, ajout des propriétés spéciales (calculées) en plus de celles disponibles par défaut
                if (qT.Name.Equals("user", StringComparison.OrdinalIgnoreCase) || qT.Name.Equals("users", StringComparison.OrdinalIgnoreCase))
                {
                    AddCalculatedProperties(adSearch, sColumns); //ajout des colonnes spéciales calculées
                }

                bool bLoaded = true;

                if (sColumns != null)
                {
                    sColumns.Clear(); // on récupère tout car dans tous les cas c'est filtré plus tard

                    if (AddSecurityMask) { adSearch.SecurityMasks = SecurityMask; }

                    mySearchResultColl = adSearch.FindAll();

                    if (mySearchResultColl.Count > 0)
                    {
                        foreach (SearchResult sr in mySearchResultColl)
                        {
                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                            foreach (DictionaryEntry property in sr.Properties)
                            {
                                if (!sColumns.Any(s => s.Name.Equals(property.Key.ToString())) && !sFakeColumns.Any(s => s.Alias.Equals(property.Key.ToString())))
                                {
                                    sColumns.Add(new Query.QField(property.Key.ToString(), property.Key.ToString(), "", "", "", 0));
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ad_addingproperty, property.Key.ToString()), SQLTools_Enums.LOG_TYPEINFO.DET);
                                }
                            }
                        }

                        foreach (Query.QField sField in sColumns)
                        {
                            dtResults.Columns.Add(sField.Alias);
                            adSearch.PropertiesToLoad.Add(sField.Alias);
                        }
                    }
                    else { bLoaded = false; }


                    if (!bLoaded)
                    {
                        Exception ex = new(Languages.Languages.ad_unabletoreadproperties);
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sADQuery, FuzibleQuery.RetryErrorOrWarning);
                        FuzibleQuery.QueryErrors += 1;
                    }
                    else
                    {
                        //je force les 2 colonnes qui servent à faire les relations avec la propriété memberOf
                        if (adSearch.PropertiesToLoad.Contains("memberof"))
                        {
                            if (!adSearch.PropertiesToLoad.Contains("name"))
                            {
                                adSearch.PropertiesToLoad.Add("name");
                            }
                            if (!adSearch.PropertiesToLoad.Contains("objectguid"))
                            {
                                adSearch.PropertiesToLoad.Add("objectguid");
                            }
                        }
                        string sFinalProps = "";
                        foreach (string prop in adSearch.PropertiesToLoad)
                        {
                            sFinalProps = string.Concat(sFinalProps, ",", prop);
                        }
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ad_addingpropertylist, sFinalProps[0..^1]), SQLTools_Enums.LOG_TYPEINFO.DET);
                        //------------------------------------------------------------------------------------

                        int cptQ = 0;

                        List<string> sListFieldsToAvoid = new();

                        foreach (SearchResult sr in mySearchResultColl)
                        {
                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                            cptQ++;
                            if (cptQ % 100 == 0) //Log de l'avancement
                            { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, string.Concat(Languages.Languages.ad_parsingdata01, cptQ.ToString(), Languages.Languages.ad_parsingdata02), SQLTools_Enums.LOG_TYPEINFO.DET); }

                            if (FuzibleQuery.QueryAnalyzer.LimitedResults > 0 && cptQ > FuzibleQuery.QueryAnalyzer.LimitedResults)
                            {
                                break;
                            }

                            DataRow dr = dtResults.NewRow();
                            System.DirectoryServices.DirectoryEntry dE = sr.GetDirectoryEntry();
                            string sValue = "";
                            IAdsLargeInteger largeInt = null;
                            long datelong = 0;

                            foreach (Query.QField sField in sColumns)
                            {
                                if (!sListFieldsToAvoid.Contains(sField.Name))
                                {
                                    if (dE.Properties[sField.Name].Value != null)
                                    {
                                        try
                                        {
                                            switch (dE.Properties[sField.Name].Value.ToString())
                                            {
                                                case "System.__ComObject":
                                                    if (sField.Name.Equals("msexchmailboxsecuritydescriptor", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        byte[] securityDescriptor = dE.Properties["msExchMailboxSecurityDescriptor"].Value as byte[];

                                                        if (securityDescriptor != null)
                                                        {
                                                            ////Instantiate an ActiveDirectorySecurity object
                                                            DirectoryObjectSecurity oSec = new ActiveDirectorySecurity();

                                                            ////Convert the security descriptor into a byte array and call the
                                                            ////SetSecurityDescriptorBinaryForm method of DirectoryObjectSecurity object 

                                                            oSec.SetSecurityDescriptorBinaryForm(securityDescriptor);
                                                            ////Get the descriptor by invoking the GetSecurityDescriptorSddlForm method
                                                            sValue = oSec.GetSecurityDescriptorSddlForm(AccessControlSections.All);

                                                            oSec = null;
                                                        }
                                                    }
                                                    else if (sField.Name.Equals("msds-keycredentiallink", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        byte[] keyCredentialLink = dE.Properties["msds-keycredentiallink"].Value as byte[];

                                                        if (keyCredentialLink != null)
                                                        {
                                                            sValue = Encoding.UTF8.GetString(keyCredentialLink);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        try
                                                        {
                                                            largeInt = null;
                                                            datelong = 0;
                                                            //string sTest = dE.Properties[sField.Name].Value.ToString();
                                                            largeInt = (IAdsLargeInteger)dE.Properties[sField.Name].Value;
                                                            datelong = (largeInt.HighPart << 32) + largeInt.LowPart;
                                                            if (datelong > 0 && datelong < long.MaxValue)
                                                            { var dT = DateTime.FromFileTimeUtc(datelong); sValue = dT.ToString(); }
                                                            else if (datelong == long.MaxValue)
                                                            { sValue = ""; }
                                                            else { sValue = ""; }

                                                        }
                                                        catch { sListFieldsToAvoid.Add(sField.Name); sValue = ""; }
                                                    }
                                                    break;

                                                case "System.Byte[]":
                                                    try
                                                    {
                                                        //Guid objectGuid = new Guid((System.Byte[])dE.Properties[sField[0]].Value);
                                                        //sValue = objectGuid.ToString();
                                                        sValue = BitConverter.ToString((System.Byte[])dE.Properties[sField.Name].Value).Replace("-", "");
                                                    }
                                                    catch { sListFieldsToAvoid.Add(sField.Name); sValue = ""; }
                                                    break;

                                                case "System.Object[]":
                                                    try
                                                    {
                                                        if (dE.Properties[sField.Name].Value is object[] objAD)
                                                        {
                                                            string[] arrO = Array.ConvertAll<object, string>(objAD, ConvertObjectToString);
                                                            if (arrO != null)
                                                            {
                                                                foreach (string sV in arrO)
                                                                { sValue = string.Concat(sValue, "[", sV, "]", ","); }
                                                            }
                                                            else { sValue = ""; }
                                                        }
                                                    }
                                                    catch { sListFieldsToAvoid.Add(sField.Name); sValue = dE.Properties[sField.Name].Value.ToString(); }
                                                    break;
                                                default:
                                                    sValue = dE.Properties[sField.Name].Value.ToString();
                                                    break;

                                            }

                                            if (sField.Name.Equals("useraccountcontrol", StringComparison.OrdinalIgnoreCase) && sValue.Length > 0)
                                            {
                                                try
                                                {
                                                    SQLTools_Enums.AD_USERACCOUNTCONTROL adc = (SQLTools_Enums.AD_USERACCOUNTCONTROL)Convert.ToInt32(sValue.ToString().ToUpper().Replace(" ", "_"));
                                                    sValue = adc.ToString().Replace("_", " ");
                                                }
                                                catch { }
                                            }

                                            //Gestion du MemberOf pour obtenir les groupes d'un utilisateur, ou les utilisateurs associés à un groupe
                                            if (FuzibleQuery.QueryAnalyzer.Tables.Count == 1) // on ne gère pas cet aspect dans les jointures
                                            {
                                                //if (sQuery.QueryAnalyzer.Tables[0].Name.Equals("users", StringComparison.OrdinalIgnoreCase))
                                                //{
                                                //côté users
                                                if (sField.Name.Equals("memberof", StringComparison.OrdinalIgnoreCase) && sValue.Length > 0)
                                                {
                                                    if (dE.Properties.Contains("objectguid") && dE.Properties.Contains("name"))
                                                    {
                                                        string sName = dE.Properties["name"].Value.ToString();
                                                        string sGuid = BitConverter.ToString((System.Byte[])dE.Properties["objectguid"].Value).Replace("-", "");
                                                        SplitGroupsMemberOf(qT.Alias, sName, sGuid, sValue, dtMemberOf);
                                                    }
                                                }
                                                //}
                                            }

                                            dr[sField.Alias] = sValue;
                                        }
                                        catch { sListFieldsToAvoid.Add(sField.Name); dr[sField.Alias] = "???"; }
                                    }
                                    else if (sr.Properties.Contains(sField.Name))
                                    {
                                        if (sField.Name.Equals("msDS-UserPasswordExpiryTimeComputed", StringComparison.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                foreach (var val in sr.Properties[sField.Name])
                                                {
                                                    long fileTime = (long)val;
                                                    DateTime expiryDate = DateTime.FromFileTimeUtc(fileTime);
                                                    sValue = expiryDate.ToString();
                                                }
                                            }
                                            catch
                                            {
                                                sValue = "";
                                            }
                                        }
                                        else
                                        {
                                            foreach (var val in sr.Properties[sField.Name])
                                            {
                                                sValue = string.Concat(sValue, ",", val.ToString());
                                            }
                                            sValue = sValue[0..^1];
                                        }
                                        dr[sField.Alias] = sValue;
                                    }
                                    else
                                    {
                                        sValue = "";
                                        dr[sField.Alias] = sValue;
                                    }
                                }
                            }

                            ////membres de chaque groupe
                            //if (sQuery.QueryAnalyzer.Tables.Count == 1) // on ne gère pas cet aspect dans les jointures
                            //{
                            //    if (sQuery.QueryAnalyzer.Tables[0].Name.Equals("groups", StringComparison.OrdinalIgnoreCase))
                            //    {
                            //        //ajout des members
                            //        if (dE.Properties.Contains("objectguid") && dE.Properties.Contains("name"))
                            //        {
                            //            string sName = dE.Properties["name"].Value.ToString();
                            //            string sGuid = BitConverter.ToString((System.Byte[])dE.Properties["objectguid"].Value).Replace("-", "");

                            //            GetListOfAdUsersByGroup(adEntry, sGuid, sName, dtMemberOf);
                            //        }
                            //    }
                            //}


                            dtResults.Rows.Add(dr);

                            dE.Close();
                        }

                        if (sListFieldsToAvoid.Count > 0)
                        {
                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_abortedfields + " : " + string.Join(",", sListFieldsToAvoid), SQLTools_Enums.LOG_TYPEINFO.WNG);
                        }
                    }

                    dtResults.Namespace = qT.Alias;

                    if (iTable > 0)
                    {
                        DataTable dtJoinedTables = null;
                        SHSOperations SHS = new(JobParameters, FuzibleQuery, ref MyLog);
                        dtJoinedTables = SHS.JoinDatatablesFromPseudoQuery(FuzibleQuery, dtMain, dtJoined, iTable);

                        if (dtJoinedTables != null)
                        {
                            dsData.Tables.Add(dtJoinedTables.Copy());
                            dtJoined.Clear();
                            dtJoined = null;
                            dtJoinedTables.Clear();
                            dtJoinedTables = null;
                        }
                    }
                }
            }

            if (iTable == 0) //en cas de table unique (pas de jointure)
            {
                dsData.Tables.Add(dtMain.Copy());
            }

            if (dtMemberOf.Rows.Count > 0)
            {
                string sColGuid = "";
                dsData.Tables.Add(dtMemberOf.Copy());
                foreach (DataColumn dc in dsData.Tables[0].Columns)
                {
                    if (dc.ColumnName.EndsWith("objectguid"))
                    { sColGuid = dc.ColumnName; break; }
                }
                if (sColGuid.Length > 0)
                {
                    dsData.Tables[0].PrimaryKey = new DataColumn[] { dsData.Tables[0].Columns[sColGuid] };

                    //ForeignKeyConstraint foreignKeyConstraint = new ForeignKeyConstraint("fk_memberof", dsData.Tables[0].Columns[sColGuid], dsData.Tables[1].Columns["objectguid"]);
                    ////Setting Rule of constraint    
                    //foreignKeyConstraint.DeleteRule = Rule.Cascade;
                    //foreignKeyConstraint.UpdateRule = Rule.Cascade;
                    //dsData.Tables[1].Constraints.Add(foreignKeyConstraint);

                    dtMemberOf.Clear();
                    dtMemberOf = null;
                }
                //set relation
            }

            dtMain.Clear();
            dtMain = null;

            return dsData;
        }

        private void AddCalculatedProperties(DirectorySearcher adSearch, List<Query.QField> sColumns)
        {
            SearchResult sr = adSearch.FindOne();
            var defaultProps = sr.Properties.PropertyNames.Cast<string>().ToList();

            //ajout de propriétés calculées
            if (sColumns.Any(c => c.Name.Equals("msDS-UserPasswordExpiryTimeComputed", StringComparison.OrdinalIgnoreCase)))
            {
                adSearch.PropertiesToLoad.Add("msDS-UserPasswordExpiryTimeComputed");
            }
            if (sColumns.Any(c => c.Name.Equals("msDS-UserPasswordExpiryTimeComputed", StringComparison.OrdinalIgnoreCase)))
            {
                adSearch.PropertiesToLoad.Add("msDS-User-Account-Control-Computed");
            }
            if (sColumns.Any(c => c.Name.Equals("tokenGroups", StringComparison.OrdinalIgnoreCase)))
            {
                adSearch.PropertiesToLoad.Add("tokenGroups");
            }

            foreach (var prop in defaultProps)
            {
                adSearch.PropertiesToLoad.Add(prop);
            }
        }

        public static string GetADQuery(CONNString Connection, string sObjectName)
        {
            return sObjectName switch
            {
                "users" => Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_SEARCH_USERS),
                "groups" => Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_SEARCH_GROUPS),
                _ => "",
            };
        }

        private void SplitGroupsMemberOf(string sObject, string sName, string sGuid, string sMemberOf, DataTable dtMemberOf)
        {
            string[] sGroups = sMemberOf.Split("],[", StringSplitOptions.RemoveEmptyEntries);

            foreach (string sGrp in sGroups)
            {
                DataRow dr = dtMemberOf.NewRow();
                dr[0] = sObject;
                dr[1] = sGuid;
                dr[2] = sName;
                dr[3] = sGrp;
                dtMemberOf.Rows.Add(dr);
            }
        }

        public static List<string> GetListOfAdUsersByGroup(System.DirectoryServices.DirectoryEntry entry, string sGuidGroup, string sGroupName, DataTable dtMemberOf)
        {
            List<string> sListMembers = new();

            //DirectoryEntry entry = new DirectoryEntry("LDAP://DC=" + domainName + ",DC=com");
            DirectorySearcher search = new(entry);
            string query = "(&(objectCategory=person)(objectClass=user)(memberOf=*))";
            search.Filter = query;
            search.PropertiesToLoad.Add("memberOf");
            search.PropertiesToLoad.Add("name");

            System.DirectoryServices.SearchResultCollection mySearchResultColl = search.FindAll();
            //Console.WriteLine("Members of the {0} Group in the {1} Domain", groupName, domainName);
            foreach (SearchResult result in mySearchResultColl)
            {
                Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                foreach (string prop in result.Properties["memberOf"])
                {
                    if (prop.Contains(sGroupName))
                    {
                        sListMembers.Add(result.Properties["name"][0].ToString());
                    }
                }
            }

            foreach (string sUser in sListMembers)
            {
                DataRow dr = dtMemberOf.NewRow();
                dr[0] = "group";
                dr[1] = sGuidGroup;
                dr[2] = sGroupName;
                dr[3] = sUser;
                dtMemberOf.Rows.Add(dr);
            }

            return sListMembers;
        }

        public void AddObjectsToAD(string sGroupsOrUsers, DataTable dtSource, Query FuzibleQuery)
        {

            dtSource.CaseSensitive = false;
            System.DirectoryServices.DirectoryEntry adEntry = ADConnection(Connection, JobParameters.DynParams);

            string sTargetOU = CleanObjectTarget(sGroupsOrUsers);

            for (int i = 0; i < dtSource.Rows.Count; i++)
            {
                try
                {
                    Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                    string sEntry = GetEntryPropertyValue(sTargetOU, dtSource.Rows[i]);

                    string sOU = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_OU);
                    string sCN = string.Concat("CN=", sEntry, ",", "OU=", sGroupsOrUsers);

                    if (sOU.Length > 0)
                    {
                        sCN = string.Concat(sCN, (sOU.StartsWith(",") ? "" : ","), sOU);
                    }

                    if (sEntry.Length > 0)
                    {
                        SearchResult sR = CheckObjectExistence(sTargetOU, sEntry);

                        //if (sR == null) { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, "null : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.DET); }

                        bool bExists = sR != null;

                        if (bExists)
                        {
                            if (JobParameters.ADTargetBehavior == 1)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_deleteobject + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.DET);
                                System.DirectoryServices.DirectoryEntry drDelete = adEntry.Children.Find(sCN, sTargetOU);
                                //suppression de l'entrée
                                if (!Convert.ToBoolean(drDelete.Properties["isCriticalSystemObject"].Value))
                                {
                                    try { adEntry.Children.Remove(drDelete); bExists = false; }
                                    catch (Exception ex)
                                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_unabletodeleteobject + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                }
                                else
                                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_dontdeletecritical + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                            }
                            else
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_ignoreobject + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.INF);
                            }
                        }

                        if ((bExists && JobParameters.ADTargetBehavior == 1) || !bExists)
                        {
                            System.DirectoryServices.DirectoryEntry newObj = adEntry.Children.Add(sCN, sTargetOU);
                            foreach (DataColumn dt in dtSource.Columns)
                            {
                                try
                                {
                                    FillObject(dtSource.Rows[i], newObj, dt.ColumnName);
                                }
                                catch (Exception ex)
                                {
                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_unabletoaddproperty + " (" + dt.ColumnName + " : " + dtSource.Rows[i][dt.ColumnName].ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
                                }
                            }

                            if (!JobParameters.RunInSimulationMode)
                            {
                                try
                                {
                                    if (AddSecurityMask) { newObj.Options.SecurityMasks = SecurityMask; }
                                    //if (JobParameters.ADActivateEntry) { newObj.Properties["userAccountControl"].Value = (int)newObj.Properties["userAccountControl"].Value & ~0x2; } //activer l'entrée                                   
                                    newObj.CommitChanges();

                                    if (JobParameters.ADActivateEntry)
                                    {
                                        try
                                        {
                                            int old_UAC = (int)newObj.Properties["userAccountControl"][0];
                                            // AD user account disable flag
                                            int ADS_UF_ACCOUNTDISABLE = 2;
                                            // To enable an ad user account, we need to clear the disable bit/flag:
                                            newObj.Properties["userAccountControl"][0] = old_UAC & ~ADS_UF_ACCOUNTDISABLE;
                                            if (AddSecurityMask) { newObj.Options.SecurityMasks = SecurityMask; }
                                            newObj.CommitChanges();
                                        }
                                        catch (Exception ex)
                                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_errorwhileactivatingentry + " : " + sEntry, CommitExceptionsAsErrors ? SQLTools_Enums.LOG_TYPEINFO.ERR : SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                    }

                                    foreach (DataColumn dc in dtSource.Rows[i].Table.Columns)
                                    {
                                        if (dc.ColumnName.Equals("setpassword", StringComparison.OrdinalIgnoreCase))
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_addobject_pwd, SQLTools_Enums.LOG_TYPEINFO.DET);
                                            newObj.Invoke("SetPassword", new object[] { dtSource.Rows[i][dc.ColumnName] });
                                            newObj.CommitChanges();
                                            break;
                                        }
                                    }

                                    newObj.Close();

                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_addobject + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.DET);
                                }
                                catch (Exception ex)
                                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_unabletoaddobject + string.Join(",", dtSource.Rows[i].ItemArray) + ")", CommitExceptionsAsErrors ? SQLTools_Enums.LOG_TYPEINFO.ERR : SQLTools_Enums.LOG_TYPEINFO.WNG); }
                            }
                            else
                            {
                                foreach (DataColumn dt in dtSource.Columns)
                                {
                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", sCN, " -> ", sTargetOU, " -> ", dtSource.Rows[i][dt.ColumnName].ToString()));
                                }
                            }
                        }
                    }
                    else
                    {
                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_missingfieldname + string.Join(",", dtSource.Rows[i].ItemArray) + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
                    }
                }
                catch (Exception ex)
                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
            }
        }

        public DataSet CompareDsWithAD(string sGroupsOrUsers, DataTable dtSource, CONNString ConnSource, Query FuzibleQuery)
        {
            DataSet dsCompare = BuildDataTables(dtSource, sGroupsOrUsers);

            try
            {
                dtSource.CaseSensitive = false;
                System.DirectoryServices.DirectoryEntry adEntry = ADConnection(ConnSource, JobParameters.DynParams);

                string sObjTarget = CleanObjectTarget(sGroupsOrUsers);

                //------------------récupération des toutes les entrées
                DirectorySearcher adSearch = new(adEntry);
                switch (sObjTarget.ToLower())
                {
                    case "user":
                        string sSearchUsers = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_SEARCH_USERS);
                        //sSearchUser = sSearchUser.Replace("[SEARCH_PROPERTY]", SearchProperty);
                        adSearch.Filter = sSearchUsers;
                        break;
                    case "group":
                        string sSearchGroups = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_SEARCH_GROUPS);
                        //sSearchGroup = sSearchGroup.Replace("[SEARCH_PROPERTY]", SearchProperty);
                        adSearch.Filter = sSearchGroups;
                        break;

                }
                adSearch.SearchScope = System.DirectoryServices.SearchScope.Subtree;
                if (AddSecurityMask) { adSearch.SecurityMasks = SecurityMask; }

                //AddCalculatedProperties(adSearch, sColumns);

                SearchResultCollection srColl = adSearch.FindAll();
                //-------------------------------------------------------

                //on établit une liste des différences :
                List<string> sListMissingInAD = new();
                List<string> sListMissingInSource = new();
                List<string> sListExistingInAD = new();

                //tentative d'ajout de clé primaire sur la source pour aller plus vite
                bool bOK = false;
                try
                {
                    if (dtSource.PrimaryKey.Length > 0) { dtSource.PrimaryKey = null; }
                    DataColumn[] dtPK = { dtSource.Columns[SearchProperty] };
                    dtSource.PrimaryKey = dtPK;
                    bOK = true;
                }
                catch (Exception ex)
                { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_cantaddprimarykeytosource + " : " + sObjTarget, SQLTools_Enums.LOG_TYPEINFO.WNG); }

                if (bOK) // on ne part pas sans clé primaire
                {
                    List<string> sPropertyValues = new();
                    foreach (SearchResult sr in srColl)
                    {
                        Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();
                        var adE = sr.GetDirectoryEntry();
                        sPropertyValues.Add(adE.Properties[SearchProperty].Value.ToString());
                    }
                    //recherche des entrées source manquantes
                    foreach (string s in sPropertyValues)
                    {
                        try
                        {
                            DataRow drTemp = null;
                            foreach (DataRow dr in dtSource.Rows)
                            {
                                if (dr[SearchProperty].ToString().Equals(s, StringComparison.OrdinalIgnoreCase))
                                {
                                    drTemp = dr;
                                    break;
                                }
                            }
                            //DataRow drTemp = dtSource.Rows.Find(s);
                            if (drTemp == null)
                            {
                                sListMissingInSource.Add(s);
                            }
                        }
                        catch (Exception ex)
                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.mt_comparedt_invalidpk01 + s + Languages.Languages.mt_comparedt_invalidpk02, FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }
                    }

                    //recherche des entrées AD manquantes
                    for (int i = 0; i < dtSource.Rows.Count; i++)
                    {
                        bool bExists = false;
                        foreach (string s in sPropertyValues)
                        {
                            if (s.Equals(dtSource.Rows[i][SearchProperty].ToString(), StringComparison.OrdinalIgnoreCase))
                            {
                                bExists = true;
                                sListExistingInAD.Add(dtSource.Rows[i][SearchProperty].ToString());
                                break;
                            }
                        }
                        if (!bExists) { sListMissingInAD.Add(dtSource.Rows[i][SearchProperty].ToString()); }
                    }

                    //tout ce qui est dans sListMissingInSource est en trop dans l'AD et doit être supprimé
                    if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("D") > -1 || JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("TAG") > -1) //autorisation de supprimer une entrée qui n'existerait pas dans la source
                    {
                        int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                        foreach (string sEntry in sListMissingInSource)
                        {
                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                            if (!JobParameters.RunInSimulationMode)
                            {
                                if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("D") > -1) //on supprime l'entrée
                                {
                                    try
                                    {
                                        System.DirectoryServices.DirectoryEntry drDelete = null;

                                        adSearch.Filter = string.Concat(SearchProperty, "=", sEntry);
                                        SearchResult searchResult = adSearch.FindOne();
                                        if (searchResult != null)
                                        { drDelete = searchResult.GetDirectoryEntry(); }

                                        AddRowToDataTable(dsCompare.Tables[2], drDelete);

                                        if (!Convert.ToBoolean(drDelete.Properties["isCriticalSystemObject"].Value))
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_deleteobject + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.INF);

                                            try { adEntry.Children.Remove(drDelete); }
                                            catch (Exception ex)
                                            { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_unabletodeleteobject + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                        }
                                        else
                                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_dontdeletecritical + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                    }
                                    catch (Exception ex)
                                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_unabletodeleteobject + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                }
                                else //on flague l'entrée comme inactive
                                {
                                    try
                                    {
                                        System.DirectoryServices.DirectoryEntry drEntry = null;

                                        adSearch.Filter = string.Concat(SearchProperty, "=", sEntry);
                                        SearchResult searchResult = adSearch.FindOne();
                                        if (searchResult != null)
                                        { drEntry = searchResult.GetDirectoryEntry(); }

                                        if (!Convert.ToBoolean(drEntry.Properties["isCriticalSystemObject"].Value))
                                        {
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_deleteobject_tag + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.INF);

                                            int old_UAC = (int)drEntry.Properties["userAccountControl"][0];
                                            // AD user account disable flag
                                            int ADS_UF_ACCOUNTDISABLE = 2;
                                            // To enable an ad user account, we need to clear the disable bit/flag:
                                            drEntry.Properties["userAccountControl"][0] = old_UAC | ADS_UF_ACCOUNTDISABLE;
                                            drEntry.Properties[TagProperty].Value = string.Concat(drEntry.Properties[TagProperty].Value, Environment.NewLine, DateTime.Now.ToShortDateString(), ":", "D");
                                            drEntry.Options.SecurityMasks = SecurityMasks.Dacl;
                                            drEntry.CommitChanges();
                                            drEntry.Close();
                                        }
                                        else
                                        { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_dontdeletecritical + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.WNG); }

                                    }
                                    catch (DirectoryServicesCOMException ex)
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_errorwhileactivatingentry + " : " + sEntry, CommitExceptionsAsErrors ? SQLTools_Enums.LOG_TYPEINFO.ERR : SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sEntry + ex.ExtendedErrorMessage == null ? "" : (" (" + ex.ExtendedErrorMessage + ")"), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    }

                                    //PrincipalContext principalContext = new PrincipalContext(ContextType.Domain);
                                    //UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(principalContext, username);
                                    //userPrincipal.Enabled = false;
                                    //userPrincipal.Save();
                                }
                            }
                            else
                            { MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", sEntry, " -> ", sObjTarget, " WILL BE DELETED ")); }
                        }

                        int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                        if (iErrWarnB > iErrWarnA)
                        {
                            FuzibleQuery.QueryAnalyzer.AddQueryProperty("DELETED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_DELETED, @"! " + sListMissingInSource.Count.ToString() + @" !", false);
                        }
                        else
                        {
                            FuzibleQuery.QueryAnalyzer.AddQueryProperty("DELETED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_DELETED, sListMissingInSource.Count.ToString(), false);
                        }
                    }

                    //tout ce qui est dans sListMissingInAD doit être ajouté dans l'AD
                    if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("I") > -1 || JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("TAG") > -1) //autorisation d'ajouter une entrée qui n'existerait pas dans la source
                    {
                        int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                        foreach (string sEntry in sListMissingInAD)
                        {
                            try
                            {
                                Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                                DataRow drTemp = dtSource.Rows.Find(sEntry);

                                string sOU = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_OU);
                                string sCN = string.Concat("CN=", sEntry, ",", "OU=", sGroupsOrUsers);
                                if (sOU.Length > 0)
                                { sCN = string.Concat(sCN, (sOU.StartsWith(",") ? "" : ","), sOU); }

                                DirectoryEntry newObj = adEntry.Children.Add(sCN, sObjTarget);

                                foreach (DataColumn dt in drTemp.Table.Columns)
                                {
                                    try
                                    {
                                        FillObject(drTemp, newObj, dt.ColumnName);
                                    }
                                    catch (Exception ex)
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_unabletoaddproperty + " (" + dt.ColumnName + " : " + drTemp[dt.ColumnName].ToString() + ")", SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    }
                                }

                                AddRowToDataTable(dsCompare.Tables[0], newObj);

                                if (!JobParameters.RunInSimulationMode)
                                {
                                    try
                                    {
                                        if (AddSecurityMask) { newObj.Options.SecurityMasks = SecurityMask; }
                                        newObj.CommitChanges();
                                        if (JobParameters.ADActivateEntry)
                                        {
                                            try
                                            {
                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_addobject + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.INF);

                                                int old_UAC = (int)newObj.Properties["userAccountControl"][0];
                                                // AD user account disable flag
                                                int ADS_UF_ACCOUNTDISABLE = 2;
                                                // To enable an ad user account, we need to clear the disable bit/flag:
                                                newObj.Properties["userAccountControl"][0] = old_UAC & ~ADS_UF_ACCOUNTDISABLE;
                                                if (AddSecurityMask) { newObj.Options.SecurityMasks = SecurityMask; }
                                                newObj.CommitChanges();
                                            }
                                            catch (Exception ex)
                                            { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_errorwhileactivatingentry + " : " + sEntry, CommitExceptionsAsErrors ? SQLTools_Enums.LOG_TYPEINFO.ERR : SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                        }

                                        foreach (DataColumn dc in drTemp.Table.Columns)
                                        {
                                            if (dc.ColumnName.Equals("setpassword", StringComparison.OrdinalIgnoreCase))
                                            {
                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_addobject_pwd, SQLTools_Enums.LOG_TYPEINFO.DET);
                                                newObj.Invoke("SetPassword", new object[] { drTemp[dc.ColumnName] });
                                                newObj.CommitChanges();
                                                break;
                                            }
                                        }

                                        newObj.Close();
                                    }
                                    catch (DirectoryServicesCOMException ex)
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_unabletoaddobject + string.Join(",", drTemp.ItemArray) + ")", CommitExceptionsAsErrors ? SQLTools_Enums.LOG_TYPEINFO.ERR : SQLTools_Enums.LOG_TYPEINFO.WNG);
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sEntry + ex.ExtendedErrorMessage == null ? "" : (" (" + ex.ExtendedErrorMessage + ")"), SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    }
                                    catch (Exception ex)
                                    {
                                        MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_unabletoaddobject + string.Join(",", drTemp.ItemArray) + ")", CommitExceptionsAsErrors ? SQLTools_Enums.LOG_TYPEINFO.ERR : SQLTools_Enums.LOG_TYPEINFO.WNG);
                                    }
                                }
                                else
                                {
                                    foreach (DataColumn dt in drTemp.Table.Columns)
                                    {
                                        MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", sCN, " -> ", sObjTarget, " -> ", drTemp[dt.ColumnName].ToString(), " WILL BE CREATED"));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, Languages.Languages.ad_unabletoaddobject + "CN = " + sEntry, SQLTools_Enums.LOG_TYPEINFO.WNG);
                            }
                        }

                        int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                        if (iErrWarnB > iErrWarnA)
                        {
                            FuzibleQuery.QueryAnalyzer.AddQueryProperty("INSERTED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, @"! " + sListMissingInAD.Count.ToString() + @" !", false);
                        }
                        else
                        {
                            FuzibleQuery.QueryAnalyzer.AddQueryProperty("INSERTED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_INSERTED, sListMissingInAD.Count.ToString(), false);
                        }
                    }

                    //tout ce qui est dans sListExistingInAD doit être comparé et modifié
                    if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("U") > -1 || JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("TAG") > -1) //autorisation de supprimer une entrée qui n'existerait pas dans la source
                    {
                        int iErrWarnA = MyLog.JobWarnings + MyLog.JobErrors;

                        foreach (string sEntry in sListExistingInAD)
                        {
                            Monitoring.TaskCancellationToken.ThrowIfCancellationRequested();

                            StringBuilder sbModified = new();

                            try
                            {
                                DataRow drData = dtSource.Rows.Find(sEntry);

                                //if (sEntry.Equals("llebay"))
                                //{

                                //}

                                System.DirectoryServices.DirectoryEntry adData = null;

                                adSearch.Filter = string.Concat(SearchProperty, "=", sEntry);
                                SearchResult searchResult = adSearch.FindOne();
                                if (searchResult != null)
                                { adData = searchResult.GetDirectoryEntry(); }

                                //using (PrincipalContext context = new PrincipalContext(ContextType.Domain))
                                //{
                                //    // Recherche de l'utilisateur par son nom
                                //    UserPrincipal user = UserPrincipal.FindByIdentity(context, sEntry);
                                //    adData = (DirectoryEntry)user.GetUnderlyingObject();
                                //}

                                //DirectoryEntry adData = adEntry.Children.Find("CN=" + sEntry, sObjTarget);
                                if (adData != null)
                                {
                                    DataRow drRow = dsCompare.Tables[1].NewRow();
                                    bool bModifiedRow = false;

                                    if (!Convert.ToBoolean(adData.Properties["isCriticalSystemObject"].Value))
                                    {
                                        foreach (DataColumn dtA in drData.Table.Columns)
                                        {
                                            if (!drData.Table.PrimaryKey.Contains(dtA))
                                            {
                                                try
                                                {
                                                    if (!JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(dtA.ColumnName))
                                                    {
                                                        string sA = drData[dtA].ToString();

                                                        bool bPropertyExists = true;
                                                        string sB = "";
                                                        string sBType = "";

                                                        try { sB = ParseADValue(adData, dtA.ColumnName, ref sA, ref sBType); }
                                                        catch { bPropertyExists = false; }

                                                        if (dtA.DataType.Equals(Type.GetType("System.String")))
                                                        {
                                                            sA = Toolbox.SetCleanString(sA, true, JobParameters.CSVCharSeparator_Target, false, ConnSource);
                                                            sB = Toolbox.SetCleanString(sB, true, JobParameters.CSVCharSeparator_Target, false, ConnSource);
                                                        }
                                                        else if (dtA.DataType.Equals(Type.GetType("System.Guid")))
                                                        {
                                                            sA = Toolbox.SetCleanString(sA, true, JobParameters.CSVCharSeparator_Target, false, ConnSource);
                                                            sB = Toolbox.SetCleanString(sB, true, JobParameters.CSVCharSeparator_Target, false, ConnSource);
                                                        }
                                                        else if (dtA.DataType.Equals(Type.GetType("System.DateTime")))
                                                        {
                                                            sA = Toolbox.SetCleanDate(sA, JobParameters, ConnSource, false, false, 2);
                                                            sB = Toolbox.SetCleanDate(sB, JobParameters, ConnSource, false, false, 2);
                                                        }
                                                        else if (dtA.DataType.Equals(Type.GetType("System.DateTimeOffset")))
                                                        {
                                                            sA = Toolbox.SetCleanDate(sA, JobParameters, ConnSource, false, false, 3);
                                                            sB = Toolbox.SetCleanDate(sB, JobParameters, ConnSource, false, false, 3);
                                                        }
                                                        else if (dtA.DataType.Equals(Type.GetType("System.Decimal")))
                                                        {
                                                            sA = Toolbox.SetCleanNumber(sA, ConnSource, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                                                            sB = Toolbox.SetCleanNumber(sB, ConnSource, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                                                        }
                                                        else if (dtA.DataType.Equals(Type.GetType("System.Boolean")))
                                                        {
                                                            sA = Toolbox.SetCleanBit(sA, SQLTools_Enums.BDD.DB_SQLSERVER, false);
                                                            sB = Toolbox.SetCleanBit(sB, SQLTools_Enums.BDD.DB_SQLSERVER, false);
                                                        }
                                                        else if (dtA.DataType.Equals(Type.GetType("System.Int16")) || dtA.DataType.Equals(Type.GetType("System.Int32")) || dtA.DataType.Equals(Type.GetType("System.Int64")) || dtA.DataType.Equals(Type.GetType("System.SByte")))
                                                        {
                                                            sA = Toolbox.SetCleanNumber(sA, ConnSource, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                                                            sB = Toolbox.SetCleanNumber(sB, ConnSource, SQLTools_Enums.TYPE_DATA.UNKNOWN, false, true, true);
                                                        }
                                                        else if (dtA.DataType.Equals(Type.GetType("System.Byte[]")))
                                                        {
                                                            sA = System.Text.Encoding.UTF8.GetString((byte[])drData[dtA]);
                                                            sB = System.Text.Encoding.UTF8.GetString((byte[])adData.Properties[dtA.ColumnName].Value);
                                                        }

                                                        drRow[dtA.ColumnName] = sB;

                                                        if (!sA.Equals(sB))
                                                        {
                                                            bModifiedRow = true;

                                                            if (bPropertyExists) //la propriété doit exister !
                                                            {
                                                                sbModified.AppendLine(string.Concat(DateTime.Now.ToShortDateString(), " - ", dtA.ColumnName, ":", sB, "->", sA));
                                                                if (!JobParameters.RunInSimulationMode)
                                                                {
                                                                    if (dtA.ColumnName.Equals("userAccountControl", StringComparison.OrdinalIgnoreCase)) //conversion de type
                                                                    {
                                                                        try
                                                                        {
                                                                            if (Regex.IsMatch(sA, "\\d+"))
                                                                            {
                                                                                adData.Properties[dtA.ColumnName].Value = Convert.ToInt32(sA);
                                                                            }
                                                                            else
                                                                            {
                                                                                var ac = (SQLTools_Enums.AD_USERACCOUNTCONTROL)Enum.Parse(typeof(SQLTools_Enums.AD_USERACCOUNTCONTROL), sA.ToUpper().Replace(" ", "_"));
                                                                                int iAc = (int)ac;
                                                                                adData.Properties[dtA.ColumnName].Value = iAc;
                                                                            }
                                                                        }
                                                                        catch { }

                                                                    }
                                                                    else
                                                                    {
                                                                        if (sA.Length == 0)
                                                                        {
                                                                            adData.Properties[dtA.ColumnName].Clear();
                                                                        }
                                                                        else
                                                                        {
                                                                            //dirEntry.Properties["userAccountControl"].Value = val & ~0x2; 
                                                                            switch (sBType)
                                                                            {
                                                                                case "System.Byte[]": //souvent les ID
                                                                                    adData.Properties[dtA.ColumnName].Value = (byte[])drData[dtA];
                                                                                    break;
                                                                                case "System.Object[]": //les listes de groupes
                                                                                    try
                                                                                    {
                                                                                        string[] sCol = sA.ToString().Split("],[", StringSplitOptions.RemoveEmptyEntries);
                                                                                        object[] oCol = new object[sCol.Length];
                                                                                        for (int i = 0; i < sCol.Length; i++)
                                                                                        {
                                                                                            if (sCol[i].StartsWith("[")) { sCol[i] = sCol[i][1..]; }
                                                                                            if (sCol[i].EndsWith("]")) { sCol[i] = sCol[i][0..^1]; }
                                                                                            oCol[i] = sCol[i];
                                                                                        }

                                                                                        if (oCol.Length > 0)
                                                                                        { adData.Properties[dtA.ColumnName].Value = oCol; }
                                                                                    }
                                                                                    catch { }              //adData.Properties[dtA.ColumnName].Value = new object[] { sA };
                                                                                    break;
                                                                                case "System.__ComObject": //les datetime
                                                                                    string sDate = Convert.ToString(Convert.ToDateTime(drData[dtA]).AddDays(1).ToFileTimeUtc());
                                                                                    adData.Properties[dtA.ColumnName].Value = sDate;
                                                                                    break;
                                                                                default:
                                                                                    adData.Properties[dtA.ColumnName].Value = sA;
                                                                                    break;
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    MyLog.WriteQueriesErrorsIntoFile(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, string.Concat("[SIMULATION MODE] ", sEntry, " -> ", sObjTarget, " : ", sB, "->", sA, " WILL BE UPDATED"));
                                                                }
                                                            }
                                                            else
                                                            {
                                                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_missingfieldname + " : " + dtA.ColumnName, SQLTools_Enums.LOG_TYPEINFO.DET);
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sEntry + " : " + dtA.ColumnName, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                                }
                                            }
                                            else
                                            {
                                                drRow[dtA.ColumnName] = drData[dtA];
                                            }
                                        }
                                        //en mode TAG, on écrit dans "description" les changements
                                        if (JobParameters.SynchroTargetTableBehavior.ToString().IndexOf("TAG") > -1)
                                        {
                                            if (!JobParameters.RunInSimulationMode)
                                            { adData.Properties[TagProperty].Value = string.Concat(adData.Properties[TagProperty].Value, "|", sbModified.ToString()); }
                                        }

                                        if (sbModified.Length > 0 && !JobParameters.RunInSimulationMode)
                                        {
                                            string sMod = sbModified.ToString();
                                            MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_updateobject + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.INF);
                                            adData.Options.SecurityMasks = SecurityMasks.Dacl;
                                            adData.CommitChanges();
                                            adData.Close();
                                        }
                                        else { adData.Close(); }

                                        if (bModifiedRow)
                                        {
                                            dsCompare.Tables[1].Rows.Add(drData.ItemArray); //voir les données qui vont être changées
                                            dsCompare.Tables[3].Rows.Add(drRow.ItemArray); //voir les anciennes données
                                        }
                                        dsCompare.Tables[4].Rows.Add(drRow.ItemArray); //voir les anciennes données
                                    }
                                    else
                                    { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, null, Languages.Languages.ad_dontupdatecritical + " : " + sEntry, SQLTools_Enums.LOG_TYPEINFO.WNG); }
                                }
                            }
                            catch (DirectoryServicesCOMException ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sEntry + ex.ExtendedErrorMessage == null ? "" : (" (" + ex.ExtendedErrorMessage + ")"), CommitExceptionsAsErrors ? SQLTools_Enums.LOG_TYPEINFO.ERR : SQLTools_Enums.LOG_TYPEINFO.WNG);
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sEntry + " : " + sbModified.ToString(), SQLTools_Enums.LOG_TYPEINFO.DBG);
                            }
                            catch (Exception ex)
                            {
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sEntry, SQLTools_Enums.LOG_TYPEINFO.WNG);
                                MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, sEntry + " : " + sbModified.ToString(), SQLTools_Enums.LOG_TYPEINFO.DBG);
                            }
                        }

                        int iErrWarnB = MyLog.JobWarnings + MyLog.JobErrors;

                        if (iErrWarnB > iErrWarnA)
                        {
                            FuzibleQuery.QueryAnalyzer.AddQueryProperty("UPDATED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_UPDATED, @"! " + sListExistingInAD.Count.ToString() + @" !", false);
                        }
                        else
                        {
                            FuzibleQuery.QueryAnalyzer.AddQueryProperty("UPDATED", SQLTools_Enums.QUERY_PROPERTIES.ROWS_UPDATED, sListExistingInAD.Count.ToString(), false);
                        }
                    }
                }
                else
                {
                    Exception ex = new(Languages.Languages.ad_cantsynchronizewithoutpk);
                    MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, SearchProperty, FuzibleQuery.RetryErrorOrWarning);
                    FuzibleQuery.QueryErrors += 1;
                }
            }
            catch (Exception ex) { MyLog.LogMessage(System.Reflection.MethodBase.GetCurrentMethod(), ClassPurpose, ex, "", FuzibleQuery.RetryErrorOrWarning); FuzibleQuery.QueryErrors += 1; }

            return dsCompare;
        }

        private void FillObject(DataRow drTemp, DirectoryEntry newObj, string sProperty)
        {
            if (!JobParameters.GlobalParameters.RESERVED_SQL_COLUMNS.Contains(sProperty))
            {
                if (sProperty.Equals("setpassword", StringComparison.OrdinalIgnoreCase)) //conversion de type
                {
                    //rien à faire, sera géré plus tard
                }
                else if (sProperty.Equals("userAccountControl", StringComparison.OrdinalIgnoreCase)) //conversion de type
                {
                    try
                    {
                        if (Regex.IsMatch(drTemp[sProperty].ToString(), "\\d+"))
                        {
                            newObj.Properties[sProperty].Value = Convert.ToInt32(drTemp[sProperty].ToString());
                        }
                        else
                        {
                            var ac = (SQLTools_Enums.AD_USERACCOUNTCONTROL)Enum.Parse(typeof(SQLTools_Enums.AD_USERACCOUNTCONTROL), drTemp[sProperty].ToString().ToUpper().Replace(" ", "_"));
                            int iAc = (int)ac;
                            newObj.Properties[sProperty].Value = iAc;
                        }
                    }
                    catch
                    {
                        throw;
                    }

                }
                else if (drTemp[sProperty].ToString().Length > 0) //on ne met pas à jour/on ne remplit pas un champ vide
                {
                    var sProp = newObj.Properties[sProperty];
                    string sBType = sProp.Value == null ? "" : sProp.Value.ToString();

                    switch (sBType)
                    {
                        case "System.__ComObject":
                            try
                            {
                                if (drTemp[sProperty].ToString().Length > 0)
                                {
                                    newObj.Properties[sProperty].Value = Convert.ToString(Convert.ToDateTime(drTemp[sProperty]).AddDays(1).ToFileTimeUtc());
                                }
                            }
                            catch { throw; }
                            break;

                        case "System.Byte[]":
                            try
                            {
                                //Guid objectGuid = new Guid((System.Byte[])dE.Properties[sField[0]].Value);
                                //sValue = objectGuid.ToString();
                                newObj.Properties[sProperty].Value = (byte[])drTemp[sProperty];
                            }
                            catch { throw; }
                            break;

                        case "System.Object[]":
                            try
                            {
                                string[] sCol = drTemp[sProperty].ToString().Split("],[", StringSplitOptions.RemoveEmptyEntries);
                                object[] oCol = new object[sCol.Length];
                                for (int i = 0; i < sCol.Length; i++)
                                {
                                    if (sCol[i].StartsWith("[")) { sCol[i] = sCol[i][1..]; }
                                    if (sCol[i].EndsWith("]")) { sCol[i] = sCol[i][0..^1]; }
                                    oCol[i] = sCol[i];
                                }

                                if (oCol.Length > 0) { newObj.Properties[sProperty].Value = oCol; }
                            }
                            catch { throw; }
                            break;

                        default:
                            newObj.Properties[sProperty].Value = drTemp[sProperty].ToString();
                            break;
                    }
                }
            }
        }

        private DataSet BuildDataTables(DataTable dtSource, string sObjTarget)
        {
            DataSet dsCompare = new DataSet(sObjTarget);

            dsCompare.Tables.Add(dtSource.Clone());
            dsCompare.Tables[0].TableName = "INSERT";
            dsCompare.Tables.Add(dtSource.Clone());
            dsCompare.Tables[1].TableName = "UPDATE";
            dsCompare.Tables.Add(dtSource.Clone());
            dsCompare.Tables[2].TableName = "DELETE";
            dsCompare.Tables.Add(dtSource.Clone());
            dsCompare.Tables[3].TableName = "UPDATE_OLDDATA";
            dsCompare.Tables.Add(dtSource.Clone());
            dsCompare.Tables[4].TableName = sObjTarget;

            foreach (DataTable dt in dsCompare.Tables)
            {
                foreach (DataColumn dc in dt.Columns)
                {
                    dc.AllowDBNull = true;
                }
            }
            return dsCompare;
        }

        private string ParseADValue(System.DirectoryServices.DirectoryEntry adData, string sColumnName, ref string sA, ref string sBType)
        {
            string sB = "";

            try
            {
                var obj = adData.Properties[sColumnName].Value;
                if (obj == null)
                {
                    sB = "";
                }
                else
                {
                    sB = obj.ToString();
                }

                sBType = obj == null ? "System.String" : obj.GetType().ToString();


                switch (sB)
                {
                    case "System.__ComObject":
                        try
                        {
                            IAdsLargeInteger largeInt = null;
                            long datelong = 0;
                            largeInt = null;
                            datelong = 0;
                            //string sTest = dE.Properties[sField.Name].Value.ToString();
                            largeInt = (IAdsLargeInteger)adData.Properties[sColumnName].Value;
                            datelong = (largeInt.HighPart << 32) + largeInt.LowPart;
                            if (datelong > 0 && datelong < long.MaxValue)
                            { var dT = DateTime.FromFileTimeUtc(datelong); sB = dT.ToString(); }
                            else if (datelong == long.MaxValue)
                            { sB = ""; DateTime.MaxValue.ToString(); }
                            else { sB = ""; }

                        }
                        catch { sB = sA; }
                        break;

                    case "System.Byte[]":
                        try
                        {
                            //Guid objectGuid = new Guid((System.Byte[])dE.Properties[sField[0]].Value);
                            //sValue = objectGuid.ToString();
                            sB = BitConverter.ToString((System.Byte[])adData.Properties[sColumnName].Value).Replace("-", "");
                        }
                        catch { sB = sA; }
                        break;

                    case "System.Object[]":
                        try
                        {
                            if (adData.Properties[sColumnName].Value is object[] objAD)
                            {
                                string[] arrO = Array.ConvertAll<object, string>(objAD, ConvertObjectToString);
                                if (arrO != null)
                                {
                                    foreach (string sV in arrO)
                                    { sB = string.Concat(sB, "[", sV, "]", ","); }
                                }
                                else { sB = sA; }
                            }
                        }
                        catch { sB = sA; }
                        break;
                }
                if (sColumnName.Equals("userAccountControl", StringComparison.OrdinalIgnoreCase))
                {
                    //sB sera forcément un int, mais pas forcément sA, on convertit
                    if (!Regex.IsMatch(sA, "\\d+"))
                    {
                        var ac = (SQLTools_Enums.AD_USERACCOUNTCONTROL)Enum.Parse(typeof(SQLTools_Enums.AD_USERACCOUNTCONTROL), sA.ToUpper().Replace(" ", "_"));
                        sA = ((int)ac).ToString();
                    }
                }

                return sB;
            }
            catch { throw; }
        }

        private void AddRowToDataTable(DataTable dt, System.DirectoryServices.DirectoryEntry adData)
        {
            var dr = dt.NewRow();
            foreach (DataColumn dc in dt.Columns)
            {
                try
                {
                    string sData = adData.Properties[dc.ColumnName].Value.ToString();
                    dr[dc.ColumnName] = sData;
                }
                catch
                {

                }
            }
            dt.Rows.Add(dr);
        }

        private System.DirectoryServices.DirectoryEntry ADConnection(CONNString CS, List<string> sListDynamicVars)
        {
            System.DirectoryServices.DirectoryEntry ldapConnection = null;
            try
            {
                if (CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_USERNAME).Length > 0)
                {
                    AuthenticationTypes adtype = AuthenticationTypes.None;

                    try { adtype = (AuthenticationTypes)Enum.Parse(typeof(AuthenticationTypes), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_AUTH)); }
                    catch { throw; }

                    ldapConnection = new System.DirectoryServices.DirectoryEntry(CS.SConnString(sListDynamicVars), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_USERNAME), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_PASSWORD), adtype);
                }
                else
                {
                    ldapConnection = new System.DirectoryServices.DirectoryEntry(CS.SConnString(sListDynamicVars));
                }
            }
            catch
            {
                throw;
            }
            return ldapConnection;
        }

        public static string CheckConnection(CONNString CS, List<string> sListDynamicVars)
        {
            string sMessage = "OK";
            string sADName;

            try
            {
                if (CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_USERNAME).Length > 0)
                {
                    AuthenticationTypes adtype = AuthenticationTypes.None;

                    try { adtype = (AuthenticationTypes)Enum.Parse(typeof(AuthenticationTypes), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_AUTH)); }
                    catch (Exception ex) { sMessage = ex.Message; }

                    System.DirectoryServices.DirectoryEntry ldapConnection = new(CS.SConnString(sListDynamicVars), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_USERNAME), CS.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_PASSWORD), adtype);
                    sADName = ldapConnection.Name;
                    ldapConnection.Close();
                }
                else
                {
                    System.DirectoryServices.DirectoryEntry ldapConnection = new(CS.SConnString(sListDynamicVars));
                    sADName = ldapConnection.Name;
                    ldapConnection.Close();
                }
            }
            catch (Exception ex)
            {
                sMessage = ex.Message;
            }

            return sMessage;
        }

        #endregion

        #region "PRIVATE VOID"

        private SearchResult CheckObjectExistence(string sObject, string sName)
        {
            SearchResult adEntry = null;

            try
            {
                System.DirectoryServices.DirectoryEntry entry = ADConnection(Connection, JobParameters.DynParams);
                DirectorySearcher adSearch = new(entry);

                string sADQuery = "";

                switch (sObject.ToLower())
                {
                    case "user":
                        sADQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_SEARCH_USER);
                        break;
                    case "group":
                        sADQuery = Connection.GetParam(SQLTools_Enums.DRIVER_PARAMS.AD_SEARCH_GROUP);
                        break;

                }
                sADQuery = sADQuery.Replace("[SEARCH_PROPERTY]", SearchProperty);
                sADQuery = sADQuery.Replace("[SEARCH_VALUE]", sName);
                adSearch.Filter = sADQuery;

                adSearch.SearchScope = System.DirectoryServices.SearchScope.Subtree;
                adEntry = adSearch.FindOne();
            }
            catch { }

            return adEntry;
        }

        private static string ConvertObjectToString(object obj)
        {
            return (obj == null) ? string.Empty : obj.ToString();
        }

        private string GetEntryPropertyValue(string sObjTarget, DataRow dr)
        {
            switch (sObjTarget.ToLower()) //en cas de mauvaise saisie
            {
                case "user":
                    if (dr.Table.Columns.Contains(SearchProperty)) { return dr[SearchProperty].ToString(); } else { return ""; }
                case "group":
                    if (dr.Table.Columns.Contains(SearchProperty)) { return dr[SearchProperty].ToString(); } else { return ""; }
                default:
                    if (dr.Table.Columns.Contains(SearchProperty)) { return dr[SearchProperty].ToString(); } else { return ""; }
            }
        }

        private static string CleanObjectTarget(string sObjTarget)
        {
            return sObjTarget.ToLower() switch //en cas de mauvaise saisie
            {
                "users" => "user",
                "groups" => "group",
                _ => "",
            };
        }

        #endregion

    }
}
