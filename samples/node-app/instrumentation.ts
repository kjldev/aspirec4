/**
 * OpenTelemetry instrumentation — must be loaded before any other module.
 *
 * Aspire automatically injects the following env vars:
 *   OTEL_SERVICE_NAME            — resource name declared in the AppHost
 *   OTEL_EXPORTER_OTLP_ENDPOINT  — OTLP endpoint on the Aspire dashboard collector
 *   OTEL_RESOURCE_ATTRIBUTES     — additional resource attributes (k=v,k=v,…)
 *
 * When those vars are absent (local dev outside Aspire) the SDK emits nothing.
 *
 * Load order: start script passes `--import ./instrumentation.ts` so this
 * module runs before index.ts and patches http/express/pg.
 */

if (process.env.OTEL_EXPORTER_OTLP_ENDPOINT) {
  const { NodeSDK } = await import('@opentelemetry/sdk-node')
  const { getNodeAutoInstrumentations } = await import(
    '@opentelemetry/auto-instrumentations-node'
  )
  const { OTLPTraceExporter } = await import(
    '@opentelemetry/exporter-trace-otlp-http'
  )
  const { OTLPMetricExporter } = await import(
    '@opentelemetry/exporter-metrics-otlp-http'
  )
  const { PeriodicExportingMetricReader } = await import(
    '@opentelemetry/sdk-metrics'
  )

  const sdk = new NodeSDK({
    traceExporter: new OTLPTraceExporter(),
    metricReader: new PeriodicExportingMetricReader({
      exporter: new OTLPMetricExporter(),
      exportIntervalMillis: 10_000,
    }),
    instrumentations: [
      getNodeAutoInstrumentations({
        // Disable noisy fs instrumentation.
        '@opentelemetry/instrumentation-fs': { enabled: false },
      }),
    ],
  })

  sdk.start()

  process.on('SIGTERM', () => sdk.shutdown().finally(() => process.exit(0)))
}
