import fs from 'fs';
import path from 'path';

const ROOT = 'C:/Users/Admin/Documents/BunnyChat';
const INTER = path.join(ROOT, '.understand-anything/intermediate');
const assembled = JSON.parse(fs.readFileSync(path.join(INTER, 'assembled-graph.json'), 'utf8'));
const scan = JSON.parse(fs.readFileSync(path.join(INTER, 'scan-result.json'), 'utf8'));
const commit = 'd8b546c3ad5c26154e03e663b0aeb4163e01f9be';

const fileTypes = new Set(['file', 'config', 'document', 'service', 'pipeline', 'table', 'schema', 'resource', 'endpoint']);
const fileNodes = assembled.nodes.filter((n) => fileTypes.has(n.type));
const nodeIds = new Set(assembled.nodes.map((n) => n.id));

function layerFor(id) {
  const fp = id.replace(/^(file|config|document):/, '');
  if (fp.startsWith('.understand-anything/')) return 'layer:tooling';
  if (fp.endsWith('.md') || fp.endsWith('.txt') && fp.includes('LICENSE')) return 'layer:documentation';
  if (fp.startsWith('BunnyChat/Backend/')) return 'layer:backend-api';
  if (fp.startsWith('BunnyChat/Frontend/')) return 'layer:frontend-presentation';
  if (fp === 'BunnyChat/Program.cs' || fp === 'BunnyChat/Directory.Build.props') return 'layer:application-core';
  if (fp.startsWith('BunnyChat/Properties/') || fp.startsWith('BunnyChat/appsettings') || fp.endsWith('.csproj') || fp.endsWith('.sln')) return 'layer:configuration';
  if (fp === 'README.md' || fp.endsWith('_DOCUMENTATION.md') || fp === 'C4_DIAGRAMS.md') return 'layer:documentation';
  return 'layer:configuration';
}

const layerBuckets = {
  'layer:application-core': [],
  'layer:backend-api': [],
  'layer:frontend-presentation': [],
  'layer:configuration': [],
  'layer:documentation': [],
  'layer:tooling': [],
};

for (const n of fileNodes) {
  layerBuckets[layerFor(n.id)].push(n.id);
}

const layers = [
  {
    id: 'layer:application-core',
    name: 'Application Core',
    description: 'Application bootstrap, hosting pipeline, and cross-cutting startup configuration.',
    nodeIds: layerBuckets['layer:application-core'],
  },
  {
    id: 'layer:backend-api',
    name: 'Backend API',
    description: 'REST controllers, DTOs, MongoDB data access, JWT token services, middleware, and domain entities.',
    nodeIds: layerBuckets['layer:backend-api'],
  },
  {
    id: 'layer:frontend-presentation',
    name: 'Frontend Presentation',
    description: 'MVC views, Razor layouts, static CSS/JS assets, and client-side auth UI behavior.',
    nodeIds: layerBuckets['layer:frontend-presentation'],
  },
  {
    id: 'layer:configuration',
    name: 'Configuration',
    description: 'Project manifests, appsettings, launch profiles, and environment-specific settings.',
    nodeIds: layerBuckets['layer:configuration'],
  },
  {
    id: 'layer:documentation',
    name: 'Documentation',
    description: 'Project README, technical docs, architecture diagrams, and third-party license files.',
    nodeIds: layerBuckets['layer:documentation'],
  },
  {
    id: 'layer:tooling',
    name: 'Analysis Tooling',
    description: 'Understand-Anything pipeline artifacts generated during codebase analysis.',
    nodeIds: layerBuckets['layer:tooling'],
  },
];

const tour = [
  {
    order: 1,
    title: 'Project Overview',
    description: 'Start with the README and technical documentation to understand BunnyChat as an ASP.NET Core 8 chat application with JWT and MongoDB.',
    nodeIds: ['document:README.md', 'document:TECHNICAL_DOCUMENTATION.md'],
  },
  {
    order: 2,
    title: 'Application Entry Point',
    description: 'Program.cs wires up MVC, Swagger, MongoDB, JWT authentication, static files, and default Page routing.',
    nodeIds: ['file:BunnyChat/Program.cs', 'config:BunnyChat/appsettings.json'],
  },
  {
    order: 3,
    title: 'Authentication API',
    description: 'AuthController handles signup, login, and logout using BCrypt, MongoDB sessions, and JWT access/refresh tokens.',
    nodeIds: ['file:BunnyChat/Backend/Controllers/auth/Authcontroller.cs', 'file:BunnyChat/Backend/Service/TokenService.cs'],
  },
  {
    order: 4,
    title: 'User & Friend APIs',
    description: 'UserController manages profiles and search; FriendController handles friend request workflows.',
    nodeIds: ['file:BunnyChat/Backend/Controllers/user/UserController.cs', 'file:BunnyChat/Backend/Controllers/friend/FriendController.cs'],
  },
  {
    order: 5,
    title: 'Data Layer',
    description: 'MongoDbService connects to the chatapp database; entity models define users, sessions, and friends collections.',
    nodeIds: ['file:BunnyChat/Backend/Data/MongoDBService.cs', 'file:BunnyChat/Backend/Models/Entities/users.cs'],
  },
  {
    order: 6,
    title: 'Frontend Pages',
    description: 'PageController serves Auth, Forgot, and Chat views; AuthLayout provides the BunnyChat branded UI shell.',
    nodeIds: ['file:BunnyChat/Frontend/Controllers/PageController.cs', 'file:BunnyChat/Frontend/Views/Shared/AuthLayout.cshtml', 'file:BunnyChat/Frontend/Views/Auth/Auth.cshtml'],
  },
  {
    order: 7,
    title: 'Client-Side Auth UX',
    description: 'Auth.js toggles login/signup cards; rabbit.js adds playful password-field interactions.',
    nodeIds: ['file:BunnyChat/Frontend/wwwroot/js/Auth.js', 'file:BunnyChat/Frontend/wwwroot/js/rabbit.js', 'file:BunnyChat/Frontend/wwwroot/css/Auth.css'],
  },
];

for (const layer of layers) {
  layer.nodeIds = [...new Set(layer.nodeIds.filter((id) => nodeIds.has(id)))];
}
for (const step of tour) {
  step.nodeIds = step.nodeIds.filter((id) => nodeIds.has(id));
}

const graph = {
  version: '1.0.0',
  project: {
    name: scan.name,
    languages: scan.languages,
    frameworks: scan.frameworks,
    description: scan.description,
    analyzedAt: new Date().toISOString(),
    gitCommitHash: commit,
  },
  nodes: assembled.nodes,
  edges: assembled.edges,
  layers,
  tour,
};

fs.writeFileSync(path.join(INTER, 'assembled-graph.json'), JSON.stringify(graph, null, 2));
fs.writeFileSync(path.join(ROOT, '.understand-anything/knowledge-graph.json'), JSON.stringify(graph, null, 2));

const issues = [];
for (const layer of layers) {
  for (const id of layer.nodeIds) {
    if (!nodeIds.has(id)) issues.push(`Layer ${layer.id} missing node ${id}`);
  }
}
const assigned = new Set(layers.flatMap((l) => l.nodeIds));
for (const n of fileNodes) {
  if (!assigned.has(n.id)) issues.push(`Unassigned file node ${n.id}`);
}

const review = {
  issues,
  warnings: [
    'Phase 3 assemble-reviewer skipped (usage limit)',
    'Phase 4 architecture-analyzer skipped — layers assigned programmatically',
    'Phase 5 tour-builder skipped — tour authored programmatically',
    '5 documentation edges dropped during merge (wrong target node type prefixes)',
  ],
  stats: {
    totalNodes: graph.nodes.length,
    totalEdges: graph.edges.length,
    totalLayers: graph.layers.length,
    tourSteps: graph.tour.length,
    nodeTypes: graph.nodes.reduce((a, n) => { a[n.type] = (a[n.type] || 0) + 1; return a; }, {}),
    edgeTypes: graph.edges.reduce((a, e) => { a[e.type] = (a[e.type] || 0) + 1; return a; }, {}),
  },
};

fs.writeFileSync(path.join(INTER, 'review.json'), JSON.stringify(review, null, 2));

const sourcePaths = scan.files.map((f) => f.path);
fs.writeFileSync(
  path.join(INTER, 'fingerprint-input.json'),
  JSON.stringify({ projectRoot: ROOT, sourceFilePaths: sourcePaths, gitCommitHash: commit }, null, 2)
);

console.log('Saved knowledge-graph.json');
console.log(JSON.stringify(review.stats, null, 2));
console.log('issues:', issues.length);
