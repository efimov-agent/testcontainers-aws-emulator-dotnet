# Based on the official AWS Emulator Dockerfile
# (docs.aws.amazon.com/emulator/latest/userguide/getting-started-docker.html); AWS publishes no prebuilt image.
# RUN curl instead of ADD <url>: the layer is cached by its instruction text, so rebuilds are offline and fast.
# `uname -m` resolves to the Docker daemon's architecture (x86_64 or aarch64), not the test host's.
FROM public.ecr.aws/amazonlinux/amazonlinux:2023-minimal
ARG EMULATOR_CHANNEL=LTS/latest
RUN curl -fsSL -o /tmp/aws-emulator.rpm \
      "https://downloads.aws.com/aws-emulator/packages/aws-emulator/${EMULATOR_CHANNEL}/aws-emulator-$(uname -m).rpm" \
 && rpm -i /tmp/aws-emulator.rpm && rm /tmp/aws-emulator.rpm \
 && mkdir -p /home/emulator && chown 1000 /home/emulator
USER 1000
ENV HOME=/home/emulator
EXPOSE 9297
ENTRYPOINT ["aws-emulator", "-b", "0.0.0.0"]
