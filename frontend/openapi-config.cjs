/** @type {import('@rtk-query/codegen-openapi').ConfigFile} */
// Reads the schema the backend emits at build time (see
// backend/Balenthiran.Snipit.WebApi.csproj → OpenApiGenerateDocumentsOnBuild), so
// `npm run codegen` works offline and in CI — no running server, no database.
// Refresh it with: cd backend && dotnet build Balenthiran.Snipit.WebApi -c Debug
const config = {
  schemaFile: '../backend/Balenthiran.Snipit.WebApi/openapi.json',
  apiFile: './src/api/emptyApi.ts',
  apiImport: 'emptySplitApi',
  outputFile: './src/api/generatedApi.ts',
  hooks: true,
};

module.exports = config;
