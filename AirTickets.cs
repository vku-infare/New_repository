namespace AirTickets
{
    using System.Collections.Generic;
    using ContentCollectorInterface;
    using Infare.DataCollection.Common;
    using Infare.DataCollection.Common.Interfaces;

    /// <summary>
    /// CC access point class
    /// IRobot needs to be implemented so CC knows how to communicate with the Filter
    /// </summary>
    public class AirTickets : IRobotPlugin
    {
        /// <summary>
        /// FilterInfo is set by CC before calling Start method
        /// </summary>
        public RobotInfo RobotInfo { get; set; }

        /// <summary>
        /// Access to Engine functionality
        /// </summary>
        internal InfareStandardFunctions ISF;

        private ExtractionLogic extractionLogic;

        /// <summary>
        /// Constructor, called when CC loads Filter DLL
        /// </summary>
        public AirTickets()
        {
            // Initialize CC API
            this.ISF = InfareStandardFunctions.Instance;
        }

        /// <summary>
        /// Set global settings for whole filter run
        /// </summary>
        public void Global()
        {
            ISF.SystemVariables.LogLevel = 320;
        }

        /// <summary>
        /// Initialize collection with given Search Criteria
        /// Content Collector notifies Filter of the Search Criteria CC intends to work with for this run 
        /// </summary>
        /// <param name="searchCriterias">Search Criterias for possible modification (empty list if CSC Type 2)</param>
        /// <returns>Either the same Search Criteria it received, or modified, e.g. sorted by POS or overwritten with test Search Criterias for a debugging session</returns>
        public IEnumerable<object> Start(IEnumerable<object> searchCriterias)
        {
            // Initialize class containing data collection logic
            this.extractionLogic = new ExtractionLogic(RobotInfo);
            return searchCriterias;
        }

        /// <summary>
        /// Extract data for given Search Criteria
        /// CC iterates over Search Criteria Collection it received from Start method and
        /// Calls this method for each Search Criteria of the Collection
        /// </summary>
        /// <param name="searchCriteria"></param>
        /// <returns>True if Data Extraction successful or no data found, False if search failed and should be researched</returns>
        public bool DataExtract(object searchCriteria)
        {
            this.extractionLogic.CollectData((SearchCriteria)searchCriteria);
            return true;
        }

        /// <summary>
        /// Wrap-up code after collection
        /// Called once from CC when work with all Search Criterias is finished
        /// </summary>
        public void End()
        {
        }
    }
}
