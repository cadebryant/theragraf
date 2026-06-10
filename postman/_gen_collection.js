// Run: node postman/_gen_collection.js
// Writes theragraf.postman_collection.json next to this file using native JSON.stringify
// (avoids PowerShell ConvertTo-Json unicode escaping that breaks Postman import).
'use strict';
const fs = require('fs');
const path = require('path');
const dest = path.join(__dirname, 'theragraf.postman_collection.json');

const sc = (exec) => ({ type: 'text/javascript', exec });

const aoaiBody = JSON.stringify({
  messages: [
    {
      role: 'system',
      content: 'You are generating realistic synthetic occupational therapy session transcripts for software testing purposes. Always use clearly fictional names. Include natural dialogue, OT-specific assessments, and functional goals. The transcript should feel like a real 20-minute session with back-and-forth conversation. You must respond with valid JSON only - no markdown, no code fences, no extra text. The JSON must have exactly two fields: therapistName (string, the full name of the therapist in the transcript) and transcript (string, the full transcript text).'
    },
    {
      role: 'user',
      content: 'Generate a realistic 20-minute occupational therapy session transcript. Use random seed {{$randomInt}} to ensure variety. Randomly pick one of these scenarios: (1) stroke patient relearning ADLs such as dressing and grooming, (2) pediatric patient with fine motor delays working on handwriting and scissor skills, (3) adult with TBI working on cognitive rehabilitation including memory and executive function, (4) elderly patient recovering from hip replacement focusing on safe transfers and home modifications, (5) child with sensory processing disorder working on sensory integration, (6) adult with hand injury doing range of motion and strengthening exercises. Use completely different fictional patient and therapist names each time. Include functional assessments with specific scores or measurements, detailed patient responses showing progress or struggle, therapist clinical reasoning, home exercise program discussion, and a plan for the next session. Return only a JSON object with fields therapistName and transcript.'
    }
  ],
  temperature: 1.2,
  max_tokens: 2500
});

const aoaiRequest = {
  method: 'POST',
  header: [
    { key: 'Content-Type', value: 'application/json' },
    { key: 'api-key', value: '{{aoaiApiKey}}' }
  ],
  url: '{{aoaiEndpoint}}/openai/deployments/{{aoaiDeployment}}/chat/completions?api-version={{aoaiApiVersion}}',
  body: { mode: 'raw', raw: aoaiBody }
};

const updateBody = JSON.stringify({
  soapNote: {
    subjective: 'Patient reports significant improvement in pain levels, now 2/10. Tolerated full session well with no fatigue complaints.',
    plan: 'Progress to phase 2 strengthening exercises. Schedule follow-up in 1 week. Patient to continue home exercise program daily.'
  },
  suggestedCptCodes: [
    { code: '97110', description: 'Therapeutic Exercise', rationale: 'Progressive strengthening to address functional deficits', billableUnits: 2 },
    { code: '97530', description: 'Therapeutic Activities', rationale: 'Functional task training for ADL independence', billableUnits: 2 }
  ]
}, null, 2);

const auth  = [{ key: 'Authorization', value: 'Bearer {{accessToken}}' }];
const authJ = [{ key: 'Content-Type', value: 'application/json' }, { key: 'Authorization', value: 'Bearer {{accessToken}}' }];

const s1test = sc([
  "const json = pm.response.json();",
  "const raw = json.choices[0].message.content;",
  "let parsed;",
  "try {",
  "    const clean = raw.replace(/^```(?:json)?\\s*/i, '').replace(/```\\s*$/,'').trim();",
  "    parsed = JSON.parse(clean);",
  "} catch (e) {",
  "    throw new Error('Step 1 response was not valid JSON. Raw content: ' + raw.substring(0, 300));",
  "}",
  "pm.collectionVariables.set('generatedTranscript', parsed.transcript);",
  "pm.collectionVariables.set('therapistName', parsed.therapistName);",
  "pm.test('Transcript generated', () => { pm.expect(parsed.transcript).to.be.a('string').and.have.length.greaterThan(100); });",
  "pm.test('Therapist name extracted', () => { pm.expect(parsed.therapistName).to.be.a('string').and.have.length.greaterThan(0); });",
  "console.log('Therapist name:', parsed.therapistName);",
  "console.log('Generated transcript:', parsed.transcript);"
]);

const s2pre = sc([
  "const transcript = pm.collectionVariables.get('generatedTranscript');",
  "const therapistName = pm.collectionVariables.get('therapistName');",
  "if (!transcript) { throw new Error('No transcript found. Run step 1 first.'); }",
  "if (!therapistName) { throw new Error('No therapist name found. Run step 1 first.'); }",
  "const names = ['alex','blake','casey','dana','drew','jamie','morgan','parker','quinn','riley','sage','taylor'];",
  "const name = names[Math.floor(Math.random() * names.length)];",
  "const suffix = Math.random().toString(36).slice(2, 6);",
  "const clientId = 'client-' + name + '-' + suffix;",
  "pm.collectionVariables.set('clientId', clientId);",
  "const body = JSON.stringify({ clientId, therapistName, sessionDate: new Date().toISOString(), discipline: 0, sessionDurationMinutes: 45, setting: 0, payer: 0, rawTranscript: transcript });",
  "pm.collectionVariables.set('requestBody', body);",
  "console.log('Client ID:', clientId);",
  "console.log('Therapist name:', therapistName);"
]);

const s2test = sc([
  "const json = pm.response.json();",
  "pm.collectionVariables.set('instanceId', json.instanceId);",
  "pm.collectionVariables.set('statusQueryGetUri', json.statusQueryGetUri);",
  "pm.test('Orchestration started', () => { pm.expect(json.instanceId).to.be.a('string'); });",
  "console.log('Instance ID:', json.instanceId);"
]);

const s3test = sc([
  "const json = pm.response.json();",
  "pm.collectionVariables.set('statusCode', json.runtimeStatus);",
  "pm.test('Status received', () => { pm.expect(['Running', 'Completed', 'Failed']).to.include(json.runtimeStatus); });",
  "console.log('Status:', json.runtimeStatus);",
  "if (json.runtimeStatus === 'Completed') {",
  "    pm.test('SOAP note has Subjective', () => pm.expect(json.output.RestoredNote.Subjective).to.be.a('string'));",
  "    pm.test('SOAP note has Objective',  () => pm.expect(json.output.RestoredNote.Objective).to.be.a('string'));",
  "    pm.test('SOAP note has Assessment', () => pm.expect(json.output.RestoredNote.Assessment).to.be.a('string'));",
  "    pm.test('SOAP note has Plan',       () => pm.expect(json.output.RestoredNote.Plan).to.be.a('string'));",
  "    pm.test('CPT codes present', () => pm.expect(json.output.SuggestedCptCodes).to.be.an('array').that.is.not.empty);",
  "    pm.test('CPT codes have required fields', () => { json.output.SuggestedCptCodes.forEach(c => { pm.expect(c.Code).to.be.a('string'); pm.expect(c.Description).to.be.a('string'); pm.expect(c.Rationale).to.be.a('string'); pm.expect(c.BillableUnits).to.be.a('number'); }); });",
  "    pm.test('ICD-10 codes present', () => pm.expect(json.output.SuggestedIcdCodes).to.be.an('array').that.is.not.empty);",
  "    pm.test('ICD-10 codes have required fields', () => { json.output.SuggestedIcdCodes.forEach(c => { pm.expect(c.Code).to.be.a('string'); pm.expect(c.Description).to.be.a('string'); pm.expect(c.Rationale).to.be.a('string'); }); });",
  "    console.log('Output:', JSON.stringify(json.output, null, 2));",
  "} else if (json.runtimeStatus === 'Failed') {",
  "    pm.test('Orchestration failed', () => { throw new Error(json.output); });",
  "}"
]);

const s4test = sc([
  "pm.test('Status 200', () => pm.response.to.have.status(200));",
  "const json = pm.response.json();",
  "pm.test('Response has items array', () => pm.expect(json.items).to.be.an('array'));",
  "pm.test('Response has pageSize',    () => pm.expect(json.pageSize).to.be.a('number'));",
  "pm.test('Response has hasMore',     () => pm.expect(json.hasMore).to.be.a('boolean'));",
  "if (json.items.length > 0) {",
  "    const first = json.items[0];",
  "    pm.test('Session has clientId',      () => pm.expect(first.clientId).to.be.a('string'));",
  "    pm.test('Session has sessionDate',   () => pm.expect(first.sessionDate).to.be.a('string'));",
  "    pm.test('Session has soapNote',      () => pm.expect(first.soapNote).to.be.an('object'));",
  "    pm.test('Session has CPT codes',     () => pm.expect(first.suggestedCptCodes).to.be.an('array'));",
  "    pm.test('Session has ICD codes',     () => pm.expect(first.suggestedIcdCodes).to.be.an('array'));",
  "    pm.test('Session has therapistName', () => pm.expect(first.therapistName).to.be.a('string'));",
  "    pm.collectionVariables.set('sessionDate', first.sessionDate);",
  "    console.log('Latest session date saved:', first.sessionDate);",
  "}",
  "if (json.continuationToken) { console.log('Next page token:', json.continuationToken); }",
  "console.log('Sessions on this page:', json.items.length, '| hasMore:', json.hasMore);"
]);

const s5test = sc([
  "pm.test('Status 200 or 404', () => { pm.expect([200, 404]).to.include(pm.response.code); });",
  "if (pm.response.code === 200) {",
  "    const json = pm.response.json();",
  "    pm.test('Has clientId',  () => pm.expect(json.clientId).to.be.a('string'));",
  "    pm.test('Has soapNote',  () => pm.expect(json.soapNote).to.be.an('object'));",
  "    pm.test('Has CPT codes', () => pm.expect(json.suggestedCptCodes).to.be.an('array'));",
  "    pm.test('Has ICD codes', () => pm.expect(json.suggestedIcdCodes).to.be.an('array'));",
  "    console.log('Session:', JSON.stringify(json, null, 2));",
  "}"
]);

const s6test = sc([
  "pm.test('Status 200', () => pm.response.to.have.status(200));",
  "const json = pm.response.json();",
  "pm.test('Has therapistName',                 () => pm.expect(json.therapistName).to.be.a('string'));",
  "pm.test('Has totalSessions',                 () => pm.expect(json.totalSessions).to.be.a('number'));",
  "pm.test('Has totalClients',                  () => pm.expect(json.totalClients).to.be.a('number'));",
  "pm.test('Has averageSessionDurationMinutes', () => pm.expect(json.averageSessionDurationMinutes).to.be.a('number'));",
  "pm.test('Has totalBillableUnits',            () => pm.expect(json.totalBillableUnits).to.be.a('number'));",
  "pm.test('Has sessionsByDiscipline',          () => pm.expect(json.sessionsByDiscipline).to.be.an('object'));",
  "pm.test('Has sessionsBySetting',             () => pm.expect(json.sessionsBySetting).to.be.an('object'));",
  "pm.test('Has sessionsByPayer',               () => pm.expect(json.sessionsByPayer).to.be.an('object'));",
  "pm.test('Has topCptCodes',                   () => pm.expect(json.topCptCodes).to.be.an('array'));",
  "pm.test('Has topIcdCodes',                   () => pm.expect(json.topIcdCodes).to.be.an('array'));",
  "console.log('Therapist stats:', JSON.stringify(json, null, 2));"
]);

const s7test = sc([
  "pm.test('Status 200', () => pm.response.to.have.status(200));",
  "const json = pm.response.json();",
  "pm.test('Has clientId',                      () => pm.expect(json.clientId).to.be.a('string'));",
  "pm.test('Has totalSessions',                 () => pm.expect(json.totalSessions).to.be.a('number'));",
  "pm.test('Has averageSessionDurationMinutes', () => pm.expect(json.averageSessionDurationMinutes).to.be.a('number'));",
  "pm.test('Has totalBillableUnits',            () => pm.expect(json.totalBillableUnits).to.be.a('number'));",
  "pm.test('Has sessionsByTherapist',           () => pm.expect(json.sessionsByTherapist).to.be.an('object'));",
  "pm.test('Has sessionsByDiscipline',          () => pm.expect(json.sessionsByDiscipline).to.be.an('object'));",
  "pm.test('Has sessionsBySetting',             () => pm.expect(json.sessionsBySetting).to.be.an('object'));",
  "pm.test('Has sessionsByPayer',               () => pm.expect(json.sessionsByPayer).to.be.an('object'));",
  "pm.test('Has topCptCodes',                   () => pm.expect(json.topCptCodes).to.be.an('array'));",
  "pm.test('Has topIcdCodes',                   () => pm.expect(json.topIcdCodes).to.be.an('array'));",
  "console.log('Client stats:', JSON.stringify(json, null, 2));"
]);

const s8test = sc([
  "pm.test('Status 200 or 404', () => pm.expect([200, 404]).to.include(pm.response.code));",
  "if (pm.response.code === 200) {",
  "    const json = pm.response.json();",
  "    pm.test('Has clientId',  () => pm.expect(json.clientId).to.be.a('string'));",
  "    pm.test('Has soapNote',  () => pm.expect(json.soapNote).to.be.an('object'));",
  "    pm.test('Has CPT codes', () => pm.expect(json.suggestedCptCodes).to.be.an('array'));",
  "    pm.test('Has ICD codes', () => pm.expect(json.suggestedIcdCodes).to.be.an('array'));",
  "    console.log('Updated session:', JSON.stringify(json, null, 2));",
  "} else {",
  "    console.log('Session not found - run steps 2-4 first to create and retrieve a session.');",
  "}"
]);

const s9test = sc([
  "pm.test('Status 204 or 404', () => pm.expect([204, 404]).to.include(pm.response.code));",
  "if (pm.response.code === 204) { console.log('Session deleted successfully.'); }",
  "else { console.log('Session not found - may have already been deleted.'); }"
]);

const tokenTest = sc([
  "const json = pm.response.json();",
  "if (json.access_token) {",
  "    pm.collectionVariables.set('accessToken', json.access_token);",
  "    pm.test('Access token acquired', () => pm.expect(json.access_token).to.be.a('string'));",
  "    console.log('Token acquired. You can now run steps 1 onwards.');",
  "} else {",
  "    pm.test('Token error: ' + json.error, () => { throw new Error(json.error_description); });",
  "}"
]);

function localSteps(base) {
  return [
    { name: '1 - Generate Synthetic OT Transcript', event: [{ listen: 'test', script: s1test }], request: aoaiRequest },
    { name: '2 - Start Documentation', event: [{ listen: 'prerequest', script: s2pre }, { listen: 'test', script: s2test }], request: { method: 'POST', header: authJ, url: `${base}/api/DocumentationStart`, body: { mode: 'raw', raw: '{{requestBody}}' } } },
    { name: '3 - Poll for Completion', event: [{ listen: 'test', script: s3test }], request: { method: 'GET', url: `${base}/runtime/webhooks/durabletask/instances/{{instanceId}}?code={{durableCode}}` } },
    { name: '4 - Get All Sessions for Client', event: [{ listen: 'test', script: s4test }], request: { method: 'GET', header: auth, url: `${base}/api/sessions/{{clientId}}` } },
    { name: '5 - Get Specific Session by Client and Date', event: [{ listen: 'test', script: s5test }], request: { method: 'GET', header: auth, url: `${base}/api/sessions/{{clientId}}/{{sessionDate}}` } },
    { name: '6 - Get Stats by Therapist', event: [{ listen: 'test', script: s6test }], request: { method: 'GET', header: auth, url: `${base}/api/stats/therapist/{{therapistName}}` } },
    { name: '7 - Get Stats by Client', event: [{ listen: 'test', script: s7test }], request: { method: 'GET', header: auth, url: `${base}/api/stats/client/{{clientId}}` } },
    { name: '8 - Update Session', event: [{ listen: 'test', script: s8test }], request: { method: 'PATCH', header: authJ, url: `${base}/api/sessions/{{clientId}}/{{sessionDate}}`, body: { mode: 'raw', raw: updateBody } } },
    { name: '9 - Delete Session', event: [{ listen: 'test', script: s9test }], request: { method: 'DELETE', header: auth, url: `${base}/api/sessions/{{clientId}}/{{sessionDate}}` } }
  ];
}

const collection = {
  info: {
    _postman_id: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
    name: 'Theragraf - Documentation Pipeline',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json'
  },
  variable: [
    { key: 'aoaiEndpoint',        value: 'https://cadehbryant-theragraf-resource.cognitiveservices.azure.com' },
    { key: 'aoaiDeployment',      value: 'gpt-4o-mini' },
    { key: 'aoaiApiVersion',      value: '2024-12-01-preview' },
    { key: 'aoaiApiKey',          value: 'YOUR_AOAI_KEY' },
    { key: 'instanceId',          value: '' },
    { key: 'statusCode',          value: '' },
    { key: 'generatedTranscript', value: '' },
    { key: 'therapistName',       value: '' },
    { key: 'sessionDate',         value: '' },
    { key: 'requestBody',         value: '' },
    { key: 'baseUrl',             value: 'http://localhost:7071' },
    { key: 'durableCode',         value: 'YOUR_LOCAL_DURABLE_CODE' },
    { key: 'liveBaseUrl',         value: 'https://theragraf-functions.azurewebsites.net' },
    { key: 'liveDurableCode',     value: 'YOUR_LIVE_DURABLE_CODE' },
    { key: 'accessToken',         value: '' },
    { key: 'deviceCode',          value: '' },
    { key: 'tenantId',            value: '9525f140-7768-4f65-8ebb-54bd5151f7cb' },
    { key: 'apiClientId',         value: 'd84a7ccd-aaa1-4adf-8211-7c03fa3d319a' },
    { key: 'clientSecret',        value: 'YOUR_CLIENT_SECRET' },
    { key: 'clientId',            value: '' }
  ],
  item: [
    {
      name: 'Local',
      item: localSteps('{{baseUrl}}')
    },
    {
      name: 'Live (Azure)',
      item: [
        {
          name: '0 - Get Token (Auto / Client Credentials)',
          event: [{ listen: 'test', script: tokenTest }],
          request: {
            method: 'POST',
            header: [{ key: 'Content-Type', value: 'application/x-www-form-urlencoded' }],
            url: 'https://login.microsoftonline.com/{{tenantId}}/oauth2/v2.0/token',
            body: {
              mode: 'urlencoded',
              urlencoded: [
                { key: 'grant_type',    value: 'client_credentials' },
                { key: 'client_id',     value: '{{apiClientId}}' },
                { key: 'client_secret', value: '{{clientSecret}}' },
                { key: 'scope',         value: 'api://{{apiClientId}}/.default' }
              ]
            }
          }
        },
        ...localSteps('{{liveBaseUrl}}').map(r => {
          // Live step 3 polls the status URI directly, not the durable webhook
          if (r.name === '3 - Poll for Completion') {
            return { ...r, request: { method: 'GET', url: '{{statusQueryGetUri}}' } };
          }
          return r;
        })
      ]
    }
  ]
};

fs.writeFileSync(dest, JSON.stringify(collection, null, 2), 'utf8');
console.log('Collection written to:', dest);
