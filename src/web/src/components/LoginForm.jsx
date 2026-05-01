import Footer from './Shared/Footer';
import Logos from './Shared/Logo';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  Button,
  Checkbox,
  Form,
  Grid,
  Header,
  Icon,
  Input,
  Message,
  Segment,
} from 'semantic-ui-react';

const initialState = {
  password: '',
  rememberMe: true,
  username: '',
};

export const getHttpsHintUrl = (location) => {
  if (!location) {
    return null;
  }

  const { hostname, protocol } = location;

  if (!hostname || protocol === 'https:') {
    return null;
  }

  return `https://${hostname}:5031`;
};

const LoginForm = ({ error, loading, onLoginAttempt }) => {
  const usernameInput = useRef();
  const [state, setState] = useState(initialState);
  const [ready, setReady] = useState(false);
  const logo = useMemo(
    () => Logos[Math.floor(Math.random() * Logos.length)],
    [],
  );
  const httpsUrl = useMemo(() => {
    if (typeof window === 'undefined') {
      return null;
    }

    return getHttpsHintUrl(window.location);
  }, []);

  useEffect(() => {
    if (state.username !== '' && state.password !== '') {
      setReady(true);
    } else {
      setReady(false);
    }
  }, [state]);

  useEffect(() => {
    usernameInput.current?.focus();
  }, [loading]);

  const handleChange = (field, value) => {
    setState({
      ...state,
      [field]: value,
    });
  };

  const { password, rememberMe, username } = state;

  return (
    <>
      <Grid
        style={{ height: 'calc(100vh - 40px)' }}
        textAlign="center"
        verticalAlign="middle"
      >
        <Grid.Column style={{ maxWidth: 460 }}>
          <Header
            as="h2"
            style={{
              fontFamily: 'monospace',
              fontSize: 'inherit',
              letterSpacing: -1,
              lineHeight: 1.1,
              whiteSpace: 'pre',
            }}
            textAlign="center"
          >
            {logo}
          </Header>
          <Form size="large">
            <Segment raised>
              <Input
                data-testid="login-username"
                disabled={loading}
                fluid
                icon="user"
                iconPosition="left"
                onChange={(event) =>
                  handleChange('username', event.target.value)
                }
                placeholder="Username"
                ref={usernameInput}
              />
              <Form.Input
                data-testid="login-password"
                disabled={loading}
                fluid
                icon="lock"
                iconPosition="left"
                onChange={(event) =>
                  handleChange('password', event.target.value)
                }
                placeholder="Password"
                type="password"
              />
              <Checkbox
                checked={rememberMe}
                disabled={loading}
                label="Remember Me"
                onChange={() => handleChange('rememberMe', !rememberMe)}
              />
            </Segment>
            <Button
              className="login-button"
              data-testid="login-submit"
              disabled={!ready || loading}
              fluid
              loading={loading}
              onClick={() => onLoginAttempt(username, password, rememberMe)}
              primary
              size="large"
            >
              <Icon name="sign in" />
              Login
            </Button>
            {error && (
              <Message
                className="login-failure"
                floating
                negative
              >
                <Icon name="x" />
                {error.message}
              </Message>
            )}
            {httpsUrl && (
              <Message
                icon
                info
                size="small"
              >
                <Icon name="shield alternate" />
                <Message.Content>
                  <Message.Header>HTTPS Option</Message.Header>
                  If your instance exposes TLS, sign in securely at{' '}
                  <a href={httpsUrl}>{httpsUrl}</a>.
                </Message.Content>
              </Message>
            )}
          </Form>
        </Grid.Column>
      </Grid>
      <Footer />
    </>
  );
};

export default LoginForm;
