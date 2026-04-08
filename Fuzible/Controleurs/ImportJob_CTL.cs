using FuzibleFramework;
using System;
using System.Collections.Generic;

namespace Fuzible.Controleurs
{
    internal class ImportJob_CTL
    {
        private string USERNAME;
        private readonly INIProgram INIFileUser;
        private INIProgram INIFile;
        public List<Job> UserJobsList
        {
            get
            {
                return INIFile.UserJobsList;
            }
        }

        public ImportJob_CTL(string sUsername)
        {
            INIFileUser = new INIProgram(sUsername, false, true);
            INIFile = new INIProgram(sUsername, false, true);
            USERNAME = sUsername;
        }

        internal List<string> GetUsers()
        {
            return INIProgram.LoadProgramUsers(USERNAME);
        }

        internal Job GetJob(string sExtJobID)
        {
            List<Job> ListJobs = INIFile.UserJobsList;
            int iIdxJob = ListJobs.FindIndex(j => j.JobID.Equals(sExtJobID));
            if (iIdxJob > -1)
            {
                Job INIP = ListJobs[iIdxJob];
                INIP.LoadJobQueries(true);
                string sPwd = INIP.JobPassword;
                string sJID = INIP.RawJobID.ToString();
                return INIP;
            }
            else { return null; }
        }

        internal void ChangeUser(string sNewUser)
        {
            USERNAME = sNewUser;
            INIFile = new INIProgram(sNewUser, false, true);
        }

        internal Job ConvertJobForUser(Job INIP, bool bUseNameAsMatchingPattern)
        {
            INIP = INIFileUser.ImportExternalJob(INIFile, INIP, bUseNameAsMatchingPattern);
            return INIP;
        }
    }
}
