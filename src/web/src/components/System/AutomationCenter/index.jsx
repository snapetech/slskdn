import {
  automationRecipes,
  getAutomationRecipeState,
  setAutomationRecipeDryRun,
  setAutomationRecipeEnabled,
} from '../../../lib/automationRecipes';
import React, { useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Card,
  Checkbox,
  Header,
  Icon,
  Label,
  Popup,
  Statistic,
} from 'semantic-ui-react';

const impactColor = (impact) => {
  if (/public/i.test(impact)) {
    return 'orange';
  }

  if (/trusted|metadata|configured|local network/i.test(impact)) {
    return 'blue';
  }

  return 'green';
};

const formatLastDryRun = (value) => {
  if (!value) {
    return 'Not run yet';
  }

  return new Date(value).toLocaleString();
};

const AutomationCenter = () => {
  const [recipeState, setRecipeState] = useState(getAutomationRecipeState);
  const summary = useMemo(() => {
    const enabled = automationRecipes.filter(
      (recipe) => recipeState[recipe.id]?.enabled,
    ).length;

    return {
      disabled: automationRecipes.length - enabled,
      enabled,
      total: automationRecipes.length,
    };
  }, [recipeState]);

  const toggleRecipe = (recipe, enabled) => {
    setRecipeState(setAutomationRecipeEnabled(recipe.id, enabled));
    toast.info(`${recipe.title} ${enabled ? 'enabled' : 'disabled'}`);
  };

  const dryRunRecipe = (recipe) => {
    setRecipeState(setAutomationRecipeDryRun(recipe.id));
    toast.info(`${recipe.title} dry run recorded`);
  };

  return (
    <div className="automation-center">
      <Header as="h3">
        <Icon name="magic" />
        <Header.Content>
          Automation Center
          <Header.Subheader>
            Every automation is visible here. Enable recipes when their dry-run output and impact fit your node.
          </Header.Subheader>
        </Header.Content>
      </Header>

      <Statistic.Group
        className="automation-summary"
        size="small"
        widths="three"
      >
        <Statistic>
          <Statistic.Value>{summary.total}</Statistic.Value>
          <Statistic.Label>Recipes</Statistic.Label>
        </Statistic>
        <Statistic color="green">
          <Statistic.Value>{summary.enabled}</Statistic.Value>
          <Statistic.Label>Enabled</Statistic.Label>
        </Statistic>
        <Statistic color="orange">
          <Statistic.Value>{summary.disabled}</Statistic.Value>
          <Statistic.Label>Visible Disabled</Statistic.Label>
        </Statistic>
      </Statistic.Group>

      <Card.Group
        className="automation-recipe-grid"
        itemsPerRow={2}
        stackable
      >
        {automationRecipes.map((recipe) => {
          const state = recipeState[recipe.id] ?? {};
          const enabled = state.enabled === true;

          return (
            <Card
              className="automation-recipe-card"
              key={recipe.id}
            >
              <Card.Content>
                <div className="automation-recipe-head">
                  <Header
                    as="h4"
                    className="automation-recipe-title"
                  >
                    <Icon name={recipe.icon} />
                    <Header.Content>{recipe.title}</Header.Content>
                  </Header>
                  <Popup
                    content={`${enabled ? 'Disable' : 'Enable'} ${recipe.title}. Disabled recipes remain visible so setup work is discoverable.`}
                    position="top center"
                    trigger={
                      <Checkbox
                        aria-label={`${enabled ? 'Disable' : 'Enable'} ${recipe.title}`}
                        checked={enabled}
                        onChange={(_event, { checked }) =>
                          toggleRecipe(recipe, checked)
                        }
                        toggle
                      />
                    }
                  />
                </div>
                <Card.Description>{recipe.description}</Card.Description>
                <div className="automation-recipe-labels">
                  <Label basic>
                    <Icon name="clock outline" />
                    {recipe.cadence}
                  </Label>
                  <Label
                    basic
                    color={impactColor(recipe.networkImpact)}
                  >
                    <Icon name="sitemap" />
                    {recipe.networkImpact}
                  </Label>
                  <Label basic>
                    <Icon name="file outline" />
                    {recipe.fileImpact}
                  </Label>
                </div>
              </Card.Content>
              <Card.Content extra>
                <div className="automation-recipe-actions">
                  <span className="automation-recipe-dry-run">
                    Dry run: {formatLastDryRun(state.lastDryRunAt)}
                  </span>
                  <Popup
                    content={`Record a dry run checkpoint for ${recipe.title}. This shell does not execute network or file actions yet.`}
                    position="top center"
                    trigger={
                      <Button
                        aria-label={`Record dry run for ${recipe.title}`}
                        basic
                        icon
                        onClick={() => dryRunRecipe(recipe)}
                        size="small"
                        title={`Record dry run for ${recipe.title}`}
                      >
                        <Icon name="play circle outline" />
                      </Button>
                    }
                  />
                </div>
              </Card.Content>
            </Card>
          );
        })}
      </Card.Group>
    </div>
  );
};

export default AutomationCenter;
