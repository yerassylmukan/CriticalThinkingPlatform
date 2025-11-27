namespace WebApi.Common;

public static class PromptTemplates
{
    public static string BuildGenerationPrompt(string question, string lang = "English")
    {
        return $@"
You are creating four distinct answers to the same question to train critical thinking.

Language rules (STRICT):
- Use exactly this output language: {lang}.
- Even if the question is written in another language, you MUST answer only in {lang}.
- Do NOT mix languages.

Question:
{question}

Produce STRICT JSON with an array 'answers'. No extra text before or after JSON.
The JSON format is:

{{
  ""answers"": [
    {{
      ""level"": ""low|medium|high"",
      ""score"": 50|75|100,
      ""text"": ""string""
    }},
    ...
  ]
}}

Requirements:
- There must be exactly 4 items in 'answers'.
- 'level' values: exactly two ""low"", one ""medium"", one ""high"".
- 'score' must match the level:
  * low    -> 50
  * medium -> 75
  * high   -> 100
- 'text' must be the answer content, written entirely in {lang}, with no other language.
- All four answers must be unique in reasoning, detail and depth.

Constraints:
- Output MUST be valid JSON only (no comments, no explanation, no Markdown).
- Do NOT include any keys other than 'level', 'score' and 'text' inside each answer object.
- Do NOT wrap JSON in backticks.
";
    }

    public static string BuildEvaluationPrompt(
        string question,
        string studentAnswer,
        IEnumerable<(string level, int score, string text)> golds)
    {
        return $@"
You are an impartial grader for middle school critical thinking.

Language rules (STRICT):
- Detect the language of the student's answer.
- Write ALL parts of your output strictly in that language (rationale, strengths, recommendations, advice).
- If the student's answer is mixed or language is ambiguous, then match the language of the question.
- Do NOT mix languages in your output.

Question:
{question}

Student answer:
{studentAnswer}

Reference set (four answers with levels and anchor scores): 
{string.Join("\n\n", golds.Select(g => $"- level: {g.level}, score: {g.score}\ntext: {g.text}"))}

Scoring policy (STRICT and EXPLICIT):
1) First, decide which single reference level the student's answer most closely matches:
   - low    (anchor score = 50)
   - medium (anchor score = 75)
   - high   (anchor score = 100)

2) Let 'base' be exactly 50, 75, or 100 depending on that chosen level.

3) Then choose an integer 'adjustment' in the closed range -5..+5 to reflect nuances such as:
   - clarity
   - use of evidence or examples
   - structure and logical coherence
   - depth of reasoning

4) Compute 'score' as:
   score = base + adjustment

5) Clamp the final 'score' into the closed range 0..100:
   - if score < 0    => set score = 0
   - if score > 100  => set score = 100

Typical ranges:
- if closest level = low    -> final score usually between 45 and 55
- if closest level = medium -> final score usually between 70 and 80
- if closest level = high   -> final score usually between 90 and 100

Tasks:
1) Briefly justify the chosen level in <= 3 sentences.
2) Provide strengths as short bullet points.
3) Provide concrete actionable recommendations as short bullet points.
4) Provide a short 'advice' paragraph with next steps.

Output STRICT JSON (no extra text, no comments, no Markdown).
JSON format EXACTLY:

{{
  ""match_level"": ""low|medium|high"",
  ""base"": 50|75|100,
  ""adjustment"": -5|-4|-3|-2|-1|0|1|2|3|4|5,
  ""score"": <integer 0..100>,
  ""rationale"": ""string (<= 3 sentences)"",
  ""strengths"": [""string"", ""string"", ...],
  ""recommendations"": [""string"", ""string"", ...],
  ""advice"": ""one short paragraph""
}}

Constraints:
- 'match_level' MUST be exactly ""low"", ""medium"" or ""high"".
- 'base' MUST be 50, 75 or 100 and must correspond to 'match_level'.
- 'adjustment' MUST be an integer between -5 and +5.
- 'score' MUST be an integer between 0 and 100 and equal to base + adjustment after clamping.
- All texts (rationale, strengths, recommendations, advice) MUST be written entirely in the detected language.
- No text is allowed outside the JSON object.
";
    }

    public static string BuildConspectPrompt(string title, IEnumerable<string> questions, string lang = "English")
    {
        var q = string.Join("\n- ", questions ?? Array.Empty<string>());
        return $@"
You are a middle-school educator. Create a comprehensive yet concise lesson conspectus on the topic below.

Language rules (STRICT):
- Use exactly this output language: {lang}.
- Even if the topic and questions are written in another language, you MUST write the conspectus only in {lang}.
- Do NOT mix languages.

Topic: {title}

Guiding questions to cover:
- {q}

Write a single continuous conspectus (no JSON), with:
- 6–10 short sections, each with a clear header,
- key definitions,
- short examples,
- common misconceptions,
- quick checks (short questions for self-test),
- 3–5 actionable tips at the end.

Constraints:
- Output MUST be plain text only (no JSON, no Markdown formatting like ``` or ###).
- All text MUST be written entirely in {lang}.
- Do not include any meta commentary about being an AI model or following instructions.
";
    }
}