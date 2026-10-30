// Node-RED settings for the sensor simulator. The broker credentials come from the environment, so the
// flow itself holds none (its broker node reads them through credsFromSettings).
module.exports = {
    flowFile: 'flows.json',
    flowFilePretty: true,
    credentialSecret: false,

    MW_CONTRIB_AMQP_USERNAME: process.env.RABBITMQ_USER,
    MW_CONTRIB_AMQP_PASSWORD: process.env.RABBITMQ_PASSWORD,

    logging: {
        console: { level: 'info', metrics: false, audit: false }
    },

    editorTheme: {
        projects: { enabled: false },
        tours: false
    }
};
