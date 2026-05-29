import fs from 'fs';
import path from 'path';

const root = 'C:/Users/Admin/Documents/doanWeb';
const scan = JSON.parse(fs.readFileSync(path.join(root, '.understand-anything/tmp/ua-scan-files.json'), 'utf8'));
const importInput = {
  projectRoot: root,
  files: scan.files,
};
fs.writeFileSync(path.join(root, '.understand-anything/tmp/ua-import-map-input.json'), JSON.stringify(importInput, null, 2));

const importOutPath = path.join(root, '.understand-anything/tmp/ua-import-map-output.json');
let importMap = {};
if (fs.existsSync(importOutPath)) {
  importMap = JSON.parse(fs.readFileSync(importOutPath, 'utf8')).importMap || {};
}

const langs = [...new Set(scan.files.map((f) => f.language))].sort();
const result = {
  name: 'DoAnWeb',
  description:
    'DoAnWeb is an ASP.NET Core 8 web application (WebMoi) with JWT authentication, MongoDB integration, and Swagger API documentation. The framework validates JWT tokens for issuer, expiration, audience, and signing authority.',
  languages: langs,
  frameworks: ['ASP.NET Core', '.NET 8', 'MongoDB', 'JWT Bearer', 'Swashbuckle'],
  files: scan.files,
  totalFiles: scan.totalFiles,
  filteredByIgnore: scan.filteredByIgnore,
  estimatedComplexity: scan.estimatedComplexity,
  importMap,
};

fs.writeFileSync(path.join(root, '.understand-anything/intermediate/scan-result.json'), JSON.stringify(result, null, 2));
console.log(`scan-result: ${result.totalFiles} files, filtered=${result.filteredByIgnore}`);
