namespace WebApi.Common;

public static class PromptTemplates
{
    public static string BuildGenerationPrompt(string question, string lang = "English")
    {
        return $@"
        You are creating four distinct answers to the same question to train critical thinking.
        Language: match the input language. If the question is in English, answer in English.

        Question:
        {question}

        Produce STRICT JSON with an array 'answers', each item:
        - 'level': one of ['low','low','medium','high'] exactly two lows, one medium, one high
        - 'score': 50 for low, 75 for medium, 100 for high
        - 'text': the answer content (unique; no overlap; correct tone for {lang})

        Constraints:
        - All four answers must be unique in reasoning and detail.
        - Do NOT include any commentary outside JSON.

        Example:
        {{""answers"":[
          {{""level"":""low"",""score"":50,""text"":""...""}},
          {{""level"":""low"",""score"":50,""text"":""...""}},
          {{""level"":""medium"",""score"":75,""text"":""...""}},
          {{""level"":""high"",""score"":100,""text"":""...""}}
        ]}}
        ";
    }

    public static string BuildEvaluationPrompt(
        string question,
        string studentAnswer,
        IEnumerable<(string level, int score, string text)> golds,
        string lang = "English")
    {
        return $@"
        You are an impartial grader for middle school critical thinking.

        Question:
        {question}

        Student answer:
        {studentAnswer}

        Reference set (four answers with levels and anchor scores): 
        {string.Join("\n\n", golds.Select(g => $"- level: {g.level}, score: {g.score}\ntext: {g.text}"))}

        Scoring policy (STRICT):
        - First, decide which single reference level the student's answer most closely matches: low (50), medium (75), or high (100).
        - Let base score be exactly 50, 75, or 100 based on that chosen level.
        - Then apply a small integer adjustment in the range -5..+5 to reflect nuances (clarity, evidence, structure).
        - Final score = base + adjustment. Final must be an integer in 0..100 (after clamping if needed).
        - Examples: 
          * if closest level = low → final typically 50..55
          * if closest level = medium → final typically 70..80 (centered at 75)
          * if closest level = high → final typically 95..100 (or 90..100 if slightly weaker)

        Tasks:
        1) Briefly justify the chosen level (<= 3 sentences).
        2) Provide strengths (bullets) and concrete actionable recommendations (bullets).
        3) Provide a short 'advice' paragraph with next steps.

        Output STRICT JSON (no extra text):
        {{
          ""match_level"": ""low|medium|high"",
          ""base"": 50|75|100,
          ""adjustment"": -5| -4| -3| -2| -1| 0| 1| 2| 3| 4| 5,
          ""score"": <integer 0..100>, 
          ""rationale"": ""<= 3 sentences"",
          ""strengths"": [""bullet 1"", ""bullet 2""],
          ""recommendations"": [""action 1"", ""action 2"", ""action 3""],
          ""advice"": ""one short paragraph""
        }}

        Language: match the student's answer language; if ambiguous, use the question's language.
        No text outside JSON.
        ";
    }

    public static string BuildConspectPrompt(string title, IEnumerable<string> questions, string lang = "English")
    {
        var q = string.Join("\n- ", questions ?? Array.Empty<string>());
        return $@"
        You are a middle-school educator. Create a comprehensive yet concise lesson conspectus on the topic below.
        Language: {lang}. The style should be clear, structured, actionable.

        Topic: {title}

        Guiding questions to cover:
        - {q}

        Write a single continuous conspectus (no JSON), with:
        - 6–10 short sections with headers,
        - key definitions, short examples, common misconceptions,
        - quick checks (questions) and 3–5 actionable tips at the end.

        Do not include any meta commentary or JSON. Output plain text only.
        ";
    }
}