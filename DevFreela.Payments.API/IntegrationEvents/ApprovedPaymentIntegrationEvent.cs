namespace DevFreela.Payments.API.IntegrationEvents
{
    public class ApprovedPaymentIntegrationEvent
    {
        public int ProjectId { get; private set; }

        public ApprovedPaymentIntegrationEvent(int projectId)
        {
            ProjectId = projectId;
        }
    }
}
