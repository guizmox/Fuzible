using FuzibleFramework;
using System.Collections.Generic;
using System.Windows;

namespace Fuzible.Controleurs
{
    internal class JobsOrganize_CTL
    {
        private readonly INIProgram INIFile;
        private readonly ServiceApp csParams;

        internal List<Job> UserJobsList;

        internal enum MODE_ACTION
        {
            SETJOB_AS_SUBJOB = 0,
            SWAP_UP = 1,
            SWAP_DOWN = 2,
            SETSUBJOB_JOB = 3
        }

        public JobsOrganize_CTL(string sUsername)
        {
            INIFile = new INIProgram(sUsername, false, false);
            UserJobsList = INIFile.UserJobsList;
            csParams = new ServiceApp(INIFile);
        }

        internal FontWeight GetJobColor(int iRawID)
        {
            return INIFile.GetChildrenCount(iRawID).Count + 1 == 1 ? FontWeights.Normal : FontWeights.Bold;
        }

        public Job GetJob(string sJobID)
        {
            return INIFile.GetJob(sJobID);
        }

        public List<Job> GetStepsFromJob(int iJobRawID)
        {
            return INIFile.GetChildrenCount(iJobRawID);
        }

        internal string MoveJob(MODE_ACTION JobAction, string sJobID, string sGenericString = "", bool bDeletePlanif = false)
        {
            string sNewJobID = "";
            Job INIP = INIFile.GetJob(sJobID);

            switch (JobAction)
            {
                case MODE_ACTION.SETJOB_AS_SUBJOB:
                    if (IsJobPlanified(sJobID) && bDeletePlanif)
                    {
                        csParams.DeleteAllPlanifsForJob(INIP.JobID);
                    }
                    else
                    {
                        sNewJobID = INIFile.SetJobAsSubJob(sJobID, sGenericString);
                        //sNewJobID = INIFile.GetJob(sNewJobID).ParentJobID;
                        csParams.UpdateAllPlanifsForJob(INIP.JobID, sNewJobID);
                    }
                    break;

                case MODE_ACTION.SWAP_UP:
                    sNewJobID = INIFile.SwapSubJob(sJobID, true);
                    //MAJ des planifs
                    List<PlanifModel> planifListU = csParams.GetJobPlanifications(INIP);
                    if (planifListU.Count > 0) { csParams.UpdateAllPlanifsForJob(INIP.JobID, sNewJobID); }
                    break;

                case MODE_ACTION.SWAP_DOWN:
                    sNewJobID = INIFile.SwapSubJob(sJobID, false);
                    List<PlanifModel> planifListD = csParams.GetJobPlanifications(INIP);
                    if (planifListD.Count > 0) { csParams.UpdateAllPlanifsForJob(INIP.JobID, sNewJobID); }
                    break;

                case MODE_ACTION.SETSUBJOB_JOB:
                    if (IsJobPlanified(sJobID) && bDeletePlanif)
                    {
                        csParams.DeleteAllPlanifsForJob(INIP.JobID);
                    }
                    else
                    {
                        sNewJobID = INIFile.SetSubJobAsMainJob(sJobID);
                        csParams.UpdateAllPlanifsForJob(INIP.JobID, sNewJobID);
                    }

                    Job J2 = INIFile.GetJob(sNewJobID);
                    J2.SetJobPassword(sGenericString, sNewJobID, false);
                    INIFile.SaveJob(J2);
                    break;
            }

            UserJobsList = INIFile.UserJobsList;

            return sNewJobID;
        }

        internal bool IsJobPlanified(string sJobID)
        {
            Job INIP = INIFile.GetJob(sJobID);
            List<PlanifModel> planifList = csParams.GetJobPlanifications(INIP);
            if (planifList.Count > 0) { return true; } else { return false; }
        }
    }
}
