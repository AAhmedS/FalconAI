namespace FalconAi.Api.Dtos
{
    public class AiScoreDto
    {
        public int AttemptId { get; set; }
        public List<AiSkillDto>? Skills { get; set; }
    }
}
