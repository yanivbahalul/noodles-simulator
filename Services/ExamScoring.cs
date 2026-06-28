namespace NoodlesSimulator.Services;

public static class ExamScoring
{
    public const int PointsPerCorrect = 6;

    public static int ScoreFromCorrectCount(int correct) => correct * PointsPerCorrect;

    public static int MaxScore(int questionCount) => questionCount * PointsPerCorrect;
}
