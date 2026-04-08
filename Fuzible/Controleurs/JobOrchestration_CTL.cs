using FuzibleFramework;
using System.Collections.Generic;
using System.Linq;

namespace Fuzible.Controleurs
{
    internal class JobOrchestration_CTL
    {
        private readonly INIProgram INIFile;
        private readonly ServiceApp csParameters;
        internal Job INIP;
        internal List<PlanifModel> Planifications;

        public List<string> DynamicParameters
        {
            get { return INIP.DynParams; }
        }

        public JobOrchestration_CTL(string sUsername, string sJobID)
        {
            INIFile = new INIProgram(sUsername, false, false);
            csParameters = new ServiceApp(INIFile);
            INIP = INIFile.GetJob(sJobID);
            Planifications = csParameters.GetJobPlanifications(INIP);
        }

        internal string CreateOrUpdatePlanification(string sCurrentPlanif, string sElements, string sDescription, string sArgs, bool bIsActive)
        {
            if (sCurrentPlanif.Length == 0)
            {

                List<PlanifModel> sExistingPlanificationsBefore = csParameters.GetJobPlanifications(INIP);
                csParameters.CreatePlanification(INIP, sElements, sDescription, sArgs, bIsActive ? 1 : 0);
                List<PlanifModel> sExistingPlanificationsAfter = csParameters.GetJobPlanifications(INIP);

                if (sExistingPlanificationsAfter.Count == sExistingPlanificationsBefore.Count + 1)
                {
                    Planifications = sExistingPlanificationsAfter;
                    return Languages.Languages.jo_msg_creationok;

                }
                else { return Languages.Languages.jo_msg_creationko; }
            }
            else
            {
                csParameters.UpdatePlanification(sCurrentPlanif, sElements, sDescription, sArgs, bIsActive ? 1 : 0);
                List<PlanifModel> sExistingPlanifications = csParameters.GetJobPlanifications(INIP);

                if (sExistingPlanifications.Any(pm => pm.PlanifID.ToString().Equals(sCurrentPlanif)))
                {
                    PlanifModel pmCheck = sExistingPlanifications.First(pm => pm.PlanifID.ToString().Equals(sCurrentPlanif));
                    if (pmCheck.PlanifPattern.Equals(sElements))
                    {
                        Planifications = sExistingPlanifications;
                        return Languages.Languages.jo_msg_updateok;
                    }
                    else { return Languages.Languages.jo_msg_updateko; }
                }
                else { return Languages.Languages.jo_msg_updateko; }
            }
        }

        internal string PlanifCollisions(string sElements, string sCurrentPlanif)
        {
            return csParameters.CheckJobCollisions(csParameters.GetListOfPlanifications(true), sElements, sCurrentPlanif.Equals("NEW") ? null : sCurrentPlanif);
        }

        internal bool PlanificationExists(string sCurrentPlanif)
        {
            return Planifications.Any(pm => pm.PlanifID.ToString().Equals(sCurrentPlanif));

        }

        internal string DeletePlanification(string sCurrentPlanif)
        {
            csParameters.DeletePlanification(sCurrentPlanif);

            List<PlanifModel> sExistingPlanificationsAfter = csParameters.GetJobPlanifications(INIP);

            if (Planifications.Count - 1 == sExistingPlanificationsAfter.Count)
            {
                Planifications = sExistingPlanificationsAfter;
                return Languages.Languages.jo_msg_deleteok;
            }
            else { return Languages.Languages.jo_msg_deleteko; }

        }

        internal string GetMonthName(int iMonth)
        {
            return INIP.GlobalParameters.Culture.DateTimeFormat.GetMonthName(iMonth);
        }
    }
}
